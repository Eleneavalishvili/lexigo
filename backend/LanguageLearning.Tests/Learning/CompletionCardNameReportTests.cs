using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Tests.Learning;

public sealed partial class CompletionCardTests
{
    [Fact]
    public async Task DeletingTheReportedAccountErasesIdentifyingReportTextAndRetainsOutcomes()
    {
        using var owner = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var reporter = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var moderator = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(owner);
        await FinishAsync(owner, setup.Token, setup.Content.LessonId);
        var card = await CardAsync(owner, setup.Token, setup.Unit);
        (await ShareAsync(owner, setup.Token, card, true)).EnsureSuccessStatusCode();
        card = await CardAsync(owner, setup.Token, setup.Unit);
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        string learnerToken = await learner.LoginAsync(reporter);
        var complaint = new CreatePublicNameReport(card.ShareCode!, $"Please review {setup.Account.Email}.");
        var created = await reporter.SendAsync(HttpMethod.Post, "/api/v1/reports/public-names", complaint, learnerToken);
        created.EnsureSuccessStatusCode();
        var sharedReport = (await created.Content.ReadFromJsonAsync<ContentReportView>())!;
        var staff = await SecurityTestAccount.CreateAsync(fixture);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(staff.Id.ToString()))!, RoleNames.Moderator)).Succeeded);
        }
        string staffToken = await staff.LoginAsync(moderator);
        Guid rankingReport;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var report = new ContentReport
            {
                ReporterId = learner.Id, LanguageId = 1, ResourceType = "RankingName", ResourceId = setup.Account.Id,
                Reason = "PublicName", Comment = setup.Account.Email, Status = "Resolved",
                Resolution = $"Reviewed {setup.Account.Email}.", CreatedAt = DateTimeOffset.UtcNow,
            };
            db.ContentReports.Add(report);
            await db.SaveChangesAsync();
            rankingReport = report.Id;
        }
        var deletionRequest = owner.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password, confirmation = "DELETE",
            recoveryCode = setup.Account.RecoveryCodes[1],
        }, setup.Token, csrf: true);
        var concurrentReport = reporter.SendAsync(HttpMethod.Post, "/api/v1/reports/public-names",
            complaint, learnerToken);
        var concurrentReview = moderator.SendAsync(HttpMethod.Patch, $"/api/v1/admin/reports/{sharedReport.Id}",
            new ReviewContentReport("Resolved", $"Reviewed private name {setup.Account.Email}."), staffToken);
        var responses = await Task.WhenAll(deletionRequest, concurrentReport, concurrentReview);
        using var deleted = responses[0];
        using var reportRetry = responses[1];
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Contains(reportRetry.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        Assert.Contains(responses[2].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var notes = await database.AuditLogs.Where(item => item.EntityType == "CompletionCard"
                && item.EntityId == card.Id.ToString()).Select(item => item.Reason).ToArrayAsync();
            Assert.All(notes, note => Assert.DoesNotContain(setup.Account.Email, note ?? ""));
        }
        using var receipt = await reporter.SendAsync(HttpMethod.Get, "/api/v1/me/reports", bearer: learnerToken);
        var reports = (await receipt.Content.ReadFromJsonAsync<ContentReportView[]>())!;
        Assert.Equal(2, reports.Length);
        Assert.All(reports, item =>
        {
            Assert.Equal(Guid.Empty, item.ResourceId);
            Assert.Equal("This report concerns a deleted account.", item.Comment);
            Assert.DoesNotContain(setup.Account.Email, item.Resolution!);
        });
        Assert.Equal("Resolved", reports.Single(item => item.Id == rankingReport).Status);
        string expectedOutcome = responses[2].StatusCode == HttpStatusCode.OK ? "Resolved" : "Rejected";
        Assert.Equal(expectedOutcome, reports.Single(item => item.Id != rankingReport).Status);
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var stored = await finalScope.ServiceProvider.GetRequiredService<LexiGoDbContext>().ContentReports
            .Where(item => item.ReporterId == learner.Id).ToArrayAsync();
        Assert.All(stored, item => Assert.DoesNotContain(setup.Account.Email, item.Comment));
        Assert.Equal(HttpStatusCode.NotFound, (await reporter.SendAsync(HttpMethod.Post,
            "/api/v1/reports/public-names", complaint, learnerToken)).StatusCode);
    }

    [Fact]
    public async Task ASharedCardNameCanBeReportedAndReviewedOnlyByModerationStaff()
    {
        const string reports = "/api/v1/reports/public-names";
        using var owner = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var reporter = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var moderator = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(owner);
        await FinishAsync(owner, setup.Token, setup.Content.LessonId);
        var card = await CardAsync(owner, setup.Token, setup.Unit);
        (await ShareAsync(owner, setup.Token, card, true)).EnsureSuccessStatusCode();
        card = await CardAsync(owner, setup.Token, setup.Unit);
        var input = new CreatePublicNameReport(card.ShareCode!, "Please review this name.");

        var learner = await SecurityTestAccount.CreateAsync(fixture);
        string learnerToken = await learner.LoginAsync(reporter);
        var staff = await SecurityTestAccount.CreateAsync(fixture);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = (await users.FindByIdAsync(staff.Id.ToString()))!;
            Assert.True((await users.AddToRoleAsync(account, RoleNames.Moderator)).Succeeded);
        }
        string staffToken = await staff.LoginAsync(moderator);

        Assert.Equal(HttpStatusCode.Unauthorized, (await reporter.SendAsync(HttpMethod.Post, reports, input)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.SendAsync(HttpMethod.Post, reports, input, setup.Token)).StatusCode);
        using var created = await reporter.SendAsync(HttpMethod.Post, reports, input, learnerToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var report = (await created.Content.ReadFromJsonAsync<ContentReportView>())!;
        Assert.Equal(("CompletionCard", "PublicName"), (report.ResourceType, report.Reason));
        Assert.Equal(HttpStatusCode.OK, (await reporter.SendAsync(HttpMethod.Post, reports, input, learnerToken)).StatusCode);
        var staffRoute = $"/api/v1/admin/reports/{report.Id}";
        var decision = new ReviewContentReport("Resolved", "Reviewed the shared card name.");
        Assert.Equal(HttpStatusCode.NotFound,
            (await reporter.SendAsync(HttpMethod.Patch, staffRoute, decision, learnerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await moderator.SendAsync(HttpMethod.Patch, staffRoute, decision, staffToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await fixture.Client.GetAsync($"/api/v1/completion-cards/shared/{card.ShareCode}")).StatusCode);
        var privateCard = await CardAsync(owner, setup.Token, setup.Unit);
        Assert.False(privateCard.IsShared);
        Assert.Equal(card.Revision + 1, privateCard.Revision);
        Assert.Equal(HttpStatusCode.OK,
            (await moderator.SendAsync(HttpMethod.Patch, staffRoute, decision, staffToken)).StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            Assert.Equal(1, await db.Notifications.CountAsync(item =>
                item.UserId == setup.Account.Id && item.Title == "Your shared card was made private"));
            Assert.Equal(1, await db.AuditLogs.CountAsync(item =>
                item.EntityId == card.Id.ToString() && item.Action == "CompletionCardModerated"));
        }
        var updated = await reporter.SendAsync(HttpMethod.Get, $"/api/v1/me/reports/{report.Id}", bearer: learnerToken);
        Assert.Equal(decision.Resolution, (await updated.Content.ReadFromJsonAsync<ContentReportView>())!.Resolution);

        Assert.Equal(HttpStatusCode.NotFound,
            (await reporter.SendAsync(HttpMethod.Post, reports, input, learnerToken)).StatusCode);
    }
}
