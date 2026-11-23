using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class DashboardTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task FreshDashboardIsPrivateAndDoesNotInventProgress()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await user.LoginAsync(browser);
        var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/dashboard", bearer: token);
        response.EnsureSuccessStatusCode();
        var value = (await response.Content.ReadFromJsonAsync<DashboardView>())!;
        value.TotalXp.Should().Be(0);
        value.TodayXp.Should().Be(0);
        value.CurrentStreak.Should().Be(0);
        value.CompletedLessons.Should().Be(0);
        value.Hearts.Count.Should().Be(5);
        value.Course.Should().BeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        string json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain(user.Email).And.NotContain("password").And.NotContain("privateEvaluation");
        (await fixture.Client.GetAsync("/api/v1/me/dashboard")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/dashboard?courseId=invalid", bearer: token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/dashboard?courseId=10000000-0000-0000-0000-000000000001", bearer: token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 0)]
    public async Task DashboardUsesOwnedLedgerAndHistoricalGoalDays(int gap, int expectedStreak)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var other = await SecurityTestAccount.CreateAsync(fixture);
        var clock = new Clock(DateTimeOffset.UtcNow);
        using var browser = new SessionTestBrowser(fixture.CreateBillingClient(clock));
        string token = await user.LoginAsync(browser);
        var spanish = await LearningTestContent.CreateAsync(fixture, user.Id);
        var italian = await LearningTestContent.CreateAsync(fixture, user.Id, courseNumber: 2);
        foreach (var id in new[] { spanish.CourseId, italian.CourseId })
            (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = id }, token)).EnsureSuccessStatusCode();
        DateOnly today = DateOnly.FromDateTime(clock.Now.UtcDateTime);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.Enrollments.Add(new() { UserId = other.Id, CourseId = spanish.CourseId, EnrolledAt = clock.Now });
            db.XpTransactions.AddRange(
                new() { UserId = user.Id, CourseId = spanish.CourseId, SourceType = "DashboardFixture", SourceId = Guid.NewGuid(), Amount = 20, EarnedAt = clock.Now },
                new() { UserId = user.Id, CourseId = italian.CourseId, SourceType = "DashboardFixture", SourceId = Guid.NewGuid(), Amount = 40, EarnedAt = clock.Now },
                new() { UserId = other.Id, CourseId = spanish.CourseId, SourceType = "DashboardFixture", SourceId = Guid.NewGuid(), Amount = 500, EarnedAt = clock.Now });
            db.LessonProgress.Add(new() { UserId = user.Id, LessonId = spanish.LessonId, Status = "Completed", BestScore = 100, CompletedAt = clock.Now });
            for (int day = gap; day < gap + 3; day++)
                db.DailyActivities.Add(new() { UserId = user.Id, LocalDate = today.AddDays(-day), EarnedXp = 20, CompletedLessons = 1, GoalReached = true });
            db.DailyActivities.Add(new() { UserId = other.Id, LocalDate = today, EarnedXp = 500, CompletedLessons = 5, GoalReached = true });
            db.DailyActivities.Add(new() { UserId = user.Id, LocalDate = today.AddDays(1), EarnedXp = 20, CompletedLessons = 1, GoalReached = true });
            var preference = await db.UserPreferences.SingleAsync(item => item.UserId == user.Id);
            preference.DailyGoalXp = 40; // Raising today's goal must not erase previously reached days.
            await db.SaveChangesAsync();
        }
        var response = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/dashboard?courseId={spanish.CourseId}&userId={other.Id}", bearer: token);
        response.EnsureSuccessStatusCode();
        var value = (await response.Content.ReadFromJsonAsync<DashboardView>())!;
        value.TotalXp.Should().Be(60);
        value.Course!.EarnedXp.Should().Be(20);
        value.CompletedLessons.Should().Be(1);
        value.Course.CompletedLessons.Should().Be(1);
        value.CurrentStreak.Should().Be(expectedStreak);
        value.DailyGoalXp.Should().Be(40);
        value.TodayXp.Should().Be(gap == 0 ? 20 : 0);
        value.GoalReached.Should().Be(gap == 0);
        clock.Now = clock.Now.AddDays(10);
        await using var laterScope = fixture.Services.CreateAsyncScope();
        var laterQueries = new DashboardQueries(laterScope.ServiceProvider.GetRequiredService<LexiGoDbContext>(),
            laterScope.ServiceProvider.GetRequiredService<HeartBalanceService>(), clock);
        (await laterQueries.GetAsync(user.Id, null, default))!.CurrentStreak.Should().Be(0);
    }

    [Fact]
    public async Task SavedTimeZoneControlsWhichDayIsToday()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var preference = await db.UserPreferences.SingleAsync(item => item.UserId == user.Id);
        preference.TimeZone = "Pacific/Kiritimati";
        db.DailyActivities.Add(new() { UserId = user.Id, LocalDate = new(2026, 11, 4), EarnedXp = 20, CompletedLessons = 1, GoalReached = true });
        await db.SaveChangesAsync();
        var clock = new Clock(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
        var queries = new DashboardQueries(db, scope.ServiceProvider.GetRequiredService<HeartBalanceService>(), clock);
        var value = (await queries.GetAsync(user.Id, null, default))!;
        value.LocalDate.Should().Be(new DateOnly(2026, 11, 4));
        value.TodayXp.Should().Be(20);
        value.CurrentStreak.Should().Be(1);
    }
}
