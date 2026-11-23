using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Tests.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Content;

public sealed partial class RewardCatalogTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string Reason = "Configure the verified learning milestones.";

    private async Task<(SecurityTestAccount User, string Token)> StaffAsync(SessionTestBrowser browser, string role)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var account = (await users.FindByIdAsync(user.Id.ToString()))!;
        if (!await users.IsInRoleAsync(account, role))
            (await users.AddToRoleAsync(account, role)).Succeeded.Should().BeTrue();
        return (user, await user.LoginAsync(browser));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static AchievementInput Badge(string? code = null) =>
        new(code ?? "test-" + Guid.NewGuid().ToString("N"), "A new milestone", "CompletedLessons", 2, Reason);

    private static ChallengeInput FutureChallenge() => new("A future challenge", "CompletedLessons", 3, 20,
        DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow.AddDays(21), Reason);

    [Theory]
    [InlineData("Learner")]
    [InlineData("Teacher")]
    [InlineData("ContentAuthor")]
    [InlineData("Reviewer")]
    [InlineData("Moderator")]
    public async Task NonAdministratorsCannotReadOrMutateGlobalRewards(string role)
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var staff = await StaffAsync(browser, role);
        foreach (string resource in new[] { "achievements", "challenges" })
        {
            var path = "/api/v1/manage/" + resource;
            (await browser.SendAsync(HttpMethod.Get, path, bearer: staff.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await browser.SendAsync(HttpMethod.Get, path + "/" + Guid.NewGuid(), bearer: staff.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await browser.SendAsync(HttpMethod.Post, path, resource == "achievements" ? (object)Badge() : FutureChallenge(), staff.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await browser.SendAsync(HttpMethod.Put, path + "/" + Guid.NewGuid(), new { revision = 1, input = Badge() }, staff.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await browser.SendAsync(HttpMethod.Delete, path + "/" + Guid.NewGuid(), new { revision = 1, reason = Reason }, staff.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task BadgeCrudUsesRevisionAuditAndPreservesEarnedHistory(string role)
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var staff = await StaffAsync(browser, role);
        var input = Badge();
        var created = await Read<IdResult>(await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/achievements", input, staff.Token));
        var path = "/api/v1/manage/achievements/" + created.Id;
        var response = await browser.SendAsync(HttpMethod.Get, path, bearer: staff.Token);
        var row = await Read<ManagedAchievement>(response);
        row.Revision.Should().Be(1);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var edits = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Put, path, new { revision = 1, input = input with { Title = "Updated title" } }, staff.Token),
            browser.SendAsync(HttpMethod.Put, path, new { revision = 1, input = input with { Title = "Another title" } }, staff.Token));
        edits.Select(item => item.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        row = await Read<ManagedAchievement>(await browser.SendAsync(HttpMethod.Get, path, bearer: staff.Token));
        row.Revision.Should().Be(2);
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        var token = await learner.LoginAsync(learnerBrowser);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.UserAchievements.Add(new UserAchievement { UserId = learner.Id, AchievementId = row.Id, EarnedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        (await browser.SendAsync(HttpMethod.Put, path, new { revision = 2, input }, staff.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await browser.SendAsync(HttpMethod.Delete, path, new { revision = 2, reason = Reason }, staff.Token)).EnsureSuccessStatusCode();
        (await browser.SendAsync(HttpMethod.Delete, path, new { revision = 2, reason = Reason }, staff.Token)).EnsureSuccessStatusCode();
        var visible = await Read<LanguageLearning.Infrastructure.Learning.AchievementView[]>(await learnerBrowser.SendAsync(HttpMethod.Get, "/api/v1/me/achievements", bearer: token));
        visible.Should().Contain(item => item.Id == row.Id && item.EarnedAt != null);
        await using var check = fixture.Services.CreateAsyncScope();
        var database = check.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.AuditLogs.CountAsync(item => item.EntityType == "Achievement" && item.EntityId == row.Id.ToString())).Should().Be(3);
        (await database.Achievements.SingleAsync(item => item.Id == row.Id)).IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task FutureChallengeCrudRejectsDuplicateWindowsAndStartedEdits()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var staff = await StaffAsync(browser, "Admin");
        var input = FutureChallenge();
        var created = await Read<IdResult>(await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/challenges", input, staff.Token));
        var path = "/api/v1/manage/challenges/" + created.Id;
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/challenges", input, staff.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await browser.SendAsync(HttpMethod.Put, path, new { revision = 1, input = input with { RewardXp = 30 } }, staff.Token)).EnsureSuccessStatusCode();
        (await browser.SendAsync(HttpMethod.Delete, path, new { revision = 1, reason = Reason }, staff.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await browser.SendAsync(HttpMethod.Delete, path, new { revision = 2, reason = Reason }, staff.Token)).EnsureSuccessStatusCode();
        var archived = await Read<ManagedChallenge>(await browser.SendAsync(HttpMethod.Get, path, bearer: staff.Token));
        archived.IsArchived.Should().BeTrue();
        archived.CanEdit.Should().BeFalse();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.Challenges.Add(new Challenge
            {
                Title = "Started fixture",
                RuleType = "CompletedLessons",
                Target = 2,
                RewardXp = 20,
                StartsAt = DateTimeOffset.UtcNow.AddHours(-1),
                EndsAt = DateTimeOffset.UtcNow.AddDays(1)
            });
            await db.SaveChangesAsync();
            var started = await db.Challenges.SingleAsync(item => item.Title == "Started fixture");
            path = "/api/v1/manage/challenges/" + started.Id;
        }
        (await browser.SendAsync(HttpMethod.Put, path, new { revision = 1, input }, staff.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await browser.SendAsync(HttpMethod.Delete, path, new { revision = 1, reason = Reason }, staff.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/manage/challenges?page=0", bearer: staff.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CatalogValidationSessionRevocationAndReadsDoNotCreateAwards()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var staff = await StaffAsync(browser, "Admin");
        (await fixture.Client.GetAsync("/api/v1/manage/achievements")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/achievements", Badge() with { RuleType = "PerfectLesson", Threshold = 2 }, staff.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/challenges", FutureChallenge() with { RewardXp = -1 }, staff.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var bad = new { code = "unknown-field", title = "Test", ruleType = "CompletedLessons", threshold = 1, reason = Reason, userId = Guid.NewGuid() };
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/achievements", bad, staff.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var page = await Read<CatalogPage<ManagedAchievement>>(await browser.SendAsync(HttpMethod.Get, "/api/v1/manage/achievements", bearer: staff.Token));
        page.Items.Length.Should().BeLessThanOrEqualTo(20);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/manage/achievements/" + Guid.NewGuid(), bearer: staff.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            (await db.UserAchievements.CountAsync(item => item.UserId == staff.User.Id)).Should().Be(0);
            (await db.XpTransactions.CountAsync(item => item.UserId == staff.User.Id)).Should().Be(0);
            await db.RefreshSessions.Where(item => item.UserId == staff.User.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.MfaVerified, false));
        }
        var denied = await browser.SendAsync(HttpMethod.Get, "/api/v1/manage/achievements", bearer: staff.Token);
        denied.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConcurrentBadgeEditAndCollectionCannotRewriteTheEarnedDefinition()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var staff = await StaffAsync(browser, "Admin");
        var input = Badge() with { Threshold = 1 };
        var created = await Read<IdResult>(await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/achievements", input, staff.Token));
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        var token = await learner.LoginAsync(learnerBrowser);
        var content = await LanguageLearning.Tests.Learning.LearningTestContent.CreateAsync(fixture, learner.Id);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.LessonProgress.Add(new LessonProgress { UserId = learner.Id, LessonId = content.LessonId, Status = "Completed", BestScore = 100, CompletedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var edit = browser.SendAsync(HttpMethod.Put, "/api/v1/manage/achievements/" + created.Id,
            new { revision = 1, input = input with { Title = "A revised milestone" } }, staff.Token);
        var collect = learnerBrowser.SendAsync(HttpMethod.Post, "/api/v1/me/achievements/collect", new { }, token);
        await Task.WhenAll(edit, collect);
        (await edit).StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);
        (await collect).EnsureSuccessStatusCode();
        var definition = await Read<ManagedAchievement>(await browser.SendAsync(HttpMethod.Get,
            "/api/v1/manage/achievements/" + created.Id, bearer: staff.Token));
        await using var check = fixture.Services.CreateAsyncScope();
        var database = check.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.UserAchievements.AnyAsync(item => item.UserId == learner.Id && item.AchievementId == created.Id)).Should().BeTrue();
        (await database.Notifications.AnyAsync(item => item.UserId == learner.Id && item.Type == "Achievement"
            && item.Title == "Achievement unlocked: " + definition.Title)).Should().BeTrue();
        definition.CanEdit.Should().BeFalse();
    }

    private sealed record IdResult(Guid Id);
}

