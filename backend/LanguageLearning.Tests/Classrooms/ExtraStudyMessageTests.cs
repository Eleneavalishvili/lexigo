using System.Net;
using LanguageLearning.Core.Billing;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Classrooms;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Classrooms;

public sealed partial class ClassroomTests
{
    [Fact]
    public async Task PrivateMessagesEnforceParticipantsReplayPagingAndClosedRequestAccess()
    {
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var outsiderBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(teacherBrowser, "Teacher");
        var learner = await Account(learnerBrowser);
        var outsider = await Account(outsiderBrowser);
        var content = await LearningTestContent.CreateAsync(fixture, teacher.User.Id);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var now = DateTimeOffset.UtcNow;
        database.Subscriptions.Add(new Subscription
        {
            UserId = learner.User.Id,
            PlanId = BillingCatalogSeed.Premium,
            CurrentPlanPriceId = await database.PlanPrices.Where(item => item.PlanId == BillingCatalogSeed.Premium)
                .Select(item => item.Id).FirstAsync(),
            Status = "Active",
            CreatedAt = now,
            CurrentPeriodStart = now.AddMinutes(-1),
            CurrentPeriodEnd = now.AddDays(7)
        });
        var offer = new ExtraStudyOffer { TeacherId = teacher.User.Id, CourseId = content.CourseId };
        var request = new ExtraStudyRequest { OfferId = offer.Id, LearnerId = learner.User.Id, Status = "Accepted", CreatedAt = now };
        database.AddRange(offer, request);
        await database.SaveChangesAsync();
        string route = $"/api/v1/extra-study/requests/{request.Id}/messages";
        var first = new SendStudyMessage(Guid.NewGuid(), "Can we practise greetings?");
        Assert.Equal(HttpStatusCode.NotFound, (await outsiderBrowser.SendAsync(HttpMethod.Get, route, bearer: outsider.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsiderBrowser.SendAsync(HttpMethod.Post, route, first, outsider.Token)).StatusCode);
        var retries = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => learnerBrowser.SendAsync(HttpMethod.Post, route, first, learner.Token)));
        Assert.All(retries, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, await database.ExtraStudyMessages.CountAsync(item => item.RequestId == request.Id));
        Assert.Equal(1, await database.Notifications.CountAsync(item => item.UserId == teacher.User.Id && item.Type == "ExtraStudyMessage"));
        Assert.Equal(HttpStatusCode.Conflict, (await learnerBrowser.SendAsync(HttpMethod.Post, route, first with { Body = "Different message" }, learner.Token)).StatusCode);
        var simultaneous = await Task.WhenAll(
            teacherBrowser.SendAsync(HttpMethod.Post, route, new SendStudyMessage(Guid.NewGuid(), "Yes, let’s begin."), teacher.Token),
            learnerBrowser.SendAsync(HttpMethod.Post, route, new SendStudyMessage(Guid.NewGuid(), "Thank you!"), learner.Token));
        Assert.All(simultaneous, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var teacherView = await Read<StudyMessagePage>(await teacherBrowser.SendAsync(HttpMethod.Get, route, bearer: teacher.Token));
        Assert.Equal(3, teacherView.Items.Length);
        Assert.Single(teacherView.Items, item => item.IsMine);
        for (int index = 0; index < 25; index++) database.ExtraStudyMessages.Add(new ExtraStudyMessage
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            SenderId = learner.User.Id,
            Body = "Earlier message " + index,
            CreatedAt = now.AddMinutes(-30 + index)
        });
        await database.SaveChangesAsync();
        var latest = await Read<StudyMessagePage>(await learnerBrowser.SendAsync(HttpMethod.Get, route, bearer: learner.Token));
        Assert.Equal(24, latest.Items.Length);
        Assert.NotNull(latest.NextBefore);
        var older = await Read<StudyMessagePage>(await learnerBrowser.SendAsync(HttpMethod.Get, route + "?before=" + latest.NextBefore, bearer: learner.Token));
        Assert.Equal(4, older.Items.Length);
        Assert.Empty(older.Items.Select(item => item.Id).Intersect(latest.Items.Select(item => item.Id)));
        Assert.Null(older.NextBefore);
        (await learnerBrowser.SendAsync(HttpMethod.Delete, $"/api/v1/extra-study/requests/{request.Id}", bearer: learner.Token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await teacherBrowser.SendAsync(HttpMethod.Post, route, new SendStudyMessage(Guid.NewGuid(), "After close"), teacher.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await learnerBrowser.SendAsync(HttpMethod.Get, route, bearer: learner.Token)).StatusCode);
    }
}
