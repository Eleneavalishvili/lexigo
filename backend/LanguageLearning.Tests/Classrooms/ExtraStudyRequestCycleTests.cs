using System.Net;
using System.Text.Json;
using LanguageLearning.Core.Billing;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Classrooms;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Classrooms;

public sealed partial class ClassroomTests
{
    [Fact]
    public async Task CancelledTeacherRequestAllowsANewRequestWithoutReopeningOldPrivateHistory()
    {
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(teacherBrowser, "Teacher");
        var learner = await Account(learnerBrowser);
        var content = await LearningTestContent.CreateAsync(fixture, teacher.User.Id);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var now = DateTimeOffset.UtcNow;
        database.Subscriptions.Add(new Subscription
        {
            UserId = learner.User.Id, PlanId = BillingCatalogSeed.Premium,
            CurrentPlanPriceId = await database.PlanPrices.Where(item => item.PlanId == BillingCatalogSeed.Premium)
                .Select(item => item.Id).FirstAsync(),
            Status = "Active", CreatedAt = now, CurrentPeriodStart = now.AddMinutes(-1), CurrentPeriodEnd = now.AddDays(7)
        });
        var offer = new ExtraStudyOffer { TeacherId = teacher.User.Id, CourseId = content.CourseId, Enabled = true };
        var old = new ExtraStudyRequest { OfferId = offer.Id, LearnerId = learner.User.Id, Status = "Accepted", CreatedAt = now };
        database.AddRange(offer, old);
        database.ExtraStudyMessages.Add(new ExtraStudyMessage
        {
            RequestId = old.Id, SenderId = teacher.User.Id, Body = "Old private study reply", CreatedAt = now
        });
        await database.SaveChangesAsync();
        string oldRoute = $"/api/v1/extra-study/requests/{old.Id}";
        (await learnerBrowser.SendAsync(HttpMethod.Delete, oldRoute, bearer: learner.Token)).EnsureSuccessStatusCode();
        string create = $"/api/v1/extra-study/offers/{offer.Id}/requests";
        var replies = await Task.WhenAll(
            learnerBrowser.SendAsync(HttpMethod.Post, create, bearer: learner.Token),
            learnerBrowser.SendAsync(HttpMethod.Post, create, bearer: learner.Token));
        var first = await Read<JsonElement>(replies[0]);
        var nextId = first.GetProperty("id").GetGuid();
        Assert.NotEqual(old.Id, nextId);
        Assert.Equal(nextId, (await Read<JsonElement>(replies[1])).GetProperty("id").GetGuid());
        Assert.Equal(2, await database.ExtraStudyRequests.CountAsync(item => item.OfferId == offer.Id));
        Assert.Equal(1, await database.Notifications.CountAsync(item => item.UserId == teacher.User.Id && item.Type == "ExtraStudyRequested"));
        Assert.Equal(HttpStatusCode.NotFound, (await learnerBrowser.SendAsync(HttpMethod.Get, oldRoute + "/messages", bearer: learner.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await learnerBrowser.SendAsync(HttpMethod.Get, oldRoute + "/space", bearer: learner.Token)).StatusCode);
        // A delayed repeat of the original cancellation cannot cancel the new request.
        (await learnerBrowser.SendAsync(HttpMethod.Delete, oldRoute, bearer: learner.Token)).EnsureSuccessStatusCode();
        var requests = await Read<ExtraStudyRequestView[]>(await learnerBrowser.SendAsync(HttpMethod.Get, "/api/v1/extra-study/requests", bearer: learner.Token));
        Assert.Contains(requests, item => item.Id == nextId && item.Status == "Pending");
        (await teacherBrowser.SendAsync(HttpMethod.Put, $"/api/v1/extra-study/requests/{nextId}/decision", new { accepted = true }, teacher.Token)).EnsureSuccessStatusCode();
        var messages = await Read<StudyMessagePage>(await learnerBrowser.SendAsync(HttpMethod.Get, $"/api/v1/extra-study/requests/{nextId}/messages", bearer: learner.Token));
        Assert.Empty(messages.Items);
        var duplicate = new ExtraStudyRequest { OfferId = offer.Id, LearnerId = learner.User.Id, CreatedAt = now };
        database.ExtraStudyRequests.Add(duplicate);
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)failure.InnerException!).SqlState);
    }
}
