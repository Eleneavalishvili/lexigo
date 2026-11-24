using System.Net;
using System.Net.Http.Json;
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
    public async Task AppointmentsAreSharedPrivateValidatedAndPreventConcurrentOverlaps()
    {
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var outsiderBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(teacherBrowser, "Teacher");
        var learner = await Account(learnerBrowser);
        var outsider = await Account(outsiderBrowser, "Teacher");
        var first = await LearningTestContent.CreateAsync(fixture, teacher.User.Id);
        var second = await LearningTestContent.CreateAsync(fixture, teacher.User.Id);
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
        // Accepted study spaces remain usable when a tutor stops taking new requests.
        // Keep this fixture unlisted so it cannot alter other directory tests.
        var offer = new ExtraStudyOffer { TeacherId = teacher.User.Id, CourseId = first.CourseId, Enabled = false };
        var request = new ExtraStudyRequest
        {
            OfferId = offer.Id,
            LearnerId = learner.User.Id,
            Status = "Accepted",
            CreatedAt = now
        };
        database.AddRange(offer, request);
        database.LessonProgress.Add(new LanguageLearning.Core.Learning.LessonProgress
        {
            UserId = learner.User.Id,
            LessonId = first.LessonId,
            Status = "Completed",
            BestScore = 90,
            CompletedAt = now
        });
        database.ExtraStudyLessons.AddRange(
            new ExtraStudyLesson { RequestId = request.Id, LessonId = first.LessonId, SharedAt = now },
            new ExtraStudyLesson { RequestId = request.Id, LessonId = second.LessonId, SharedAt = now });
        await database.SaveChangesAsync();
        string space = $"/api/v1/extra-study/requests/{request.Id}/space";
        string Schedule(Guid lesson) => $"{space}/lessons/{lesson}/schedule";
        var input = new ExtraStudyScheduleInput(now.AddDays(1), 45);
        Assert.Equal(HttpStatusCode.NotFound, (await learnerBrowser.SendAsync(HttpMethod.Put,
            Schedule(first.LessonId), input, learner.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsiderBrowser.SendAsync(HttpMethod.Put,
            Schedule(first.LessonId), input, outsider.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await teacherBrowser.SendAsync(HttpMethod.Put,
            Schedule(first.LessonId), input with { DurationMinutes = 0 }, teacher.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await teacherBrowser.SendAsync(HttpMethod.Put,
            Schedule(first.LessonId), input with { ScheduledAt = now.AddDays(-1) }, teacher.Token)).StatusCode);
        var concurrent = await Task.WhenAll(
            teacherBrowser.SendAsync(HttpMethod.Put, Schedule(first.LessonId), input, teacher.Token),
            teacherBrowser.SendAsync(HttpMethod.Put, Schedule(second.LessonId), input, teacher.Token));
        Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.Conflict);
        var view = await Read<ExtraStudySpaceView>(await learnerBrowser.SendAsync(HttpMethod.Get, space, bearer: learner.Token));
        var appointment = Assert.Single(view.Lessons, item => item.ScheduledAt != null);
        Assert.Equal("Completed", view.Lessons.Single(item => item.Id == first.LessonId).Status);
        Assert.Equal(90, view.Lessons.Single(item => item.Id == first.LessonId).BestScore);
        Assert.Null(view.Lessons.Single(item => item.Id == second.LessonId).BestScore);
        Assert.Equal(input.ScheduledAt!.Value.UtcTicks / 10, appointment.ScheduledAt!.Value.UtcTicks / 10);
        Assert.Equal(45, appointment.DurationMinutes);
        Assert.Equal(2, await database.Notifications.CountAsync(item =>
            (item.UserId == teacher.User.Id || item.UserId == learner.User.Id) && item.Type == "ExtraStudySchedule"));
        (await teacherBrowser.SendAsync(HttpMethod.Put, Schedule(appointment.Id), input, teacher.Token)).EnsureSuccessStatusCode();
        Assert.Equal(2, await database.Notifications.CountAsync(item =>
            (item.UserId == teacher.User.Id || item.UserId == learner.User.Id) && item.Type == "ExtraStudySchedule"));
        (await teacherBrowser.SendAsync(HttpMethod.Put, Schedule(appointment.Id),
            new ExtraStudyScheduleInput(null, null), teacher.Token)).EnsureSuccessStatusCode();
        var other = view.Lessons.Single(item => item.Id != appointment.Id);
        (await teacherBrowser.SendAsync(HttpMethod.Put, Schedule(other.Id), input, teacher.Token)).EnsureSuccessStatusCode();
        var cancelled = await Read<ExtraStudySpaceView>(await learnerBrowser.SendAsync(HttpMethod.Get, space, bearer: learner.Token));
        Assert.Null(cancelled.Lessons.Single(item => item.Id == appointment.Id).ScheduledAt);
        Assert.NotNull(cancelled.Lessons.Single(item => item.Id == other.Id).ScheduledAt);
    }
}
