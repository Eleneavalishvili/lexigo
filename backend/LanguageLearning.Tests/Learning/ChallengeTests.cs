using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class ChallengeTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    private readonly List<Guid> customChallenges = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Each case removes its custom catalog entries from the shared disposable fixture.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await database.UserChallenges.Where(item => customChallenges.Contains(item.ChallengeId)).ExecuteDeleteAsync();
        await database.XpTransactions.Where(item => item.SourceType == "Challenge" && customChallenges.Contains(item.SourceId)).ExecuteDeleteAsync();
        await database.Challenges.Where(item => customChallenges.Contains(item.Id)).ExecuteDeleteAsync();
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<SessionView> AnswerAsync(SessionTestBrowser browser, string token, Guid lessonId, string mode = "Lesson")
    {
        var session = mode == "Practice"
            ? await Read<SessionView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", new { lessonId }, token))
            : await Read<SessionView>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lessonId}/sessions", bearer: token));
        while (session.CurrentExercise is { } exercise)
        {
            (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
                new { sessionExerciseId = exercise.Id, answerText = "Hola", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
            session = await Read<SessionView>(await browser.SendAsync(HttpMethod.Get,
                $"/api/v1/me/learning-sessions/{session.Id}", bearer: token));
        }
        return session;
    }

    [Fact]
    public async Task WeeklyCatalogIsConcurrentIdempotentAndUsesMondayUtc()
    {
        var clock = new Clock(DateTimeOffset.Parse("2028-01-06T23:00:00Z"));
        async Task Ensure()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            await new WeeklyChallengeCatalog(scope.ServiceProvider.GetRequiredService<LexiGoDbContext>(), clock)
                .EnsureCurrentWeekAsync(CancellationToken.None);
        }
        await Task.WhenAll(Ensure(), Ensure());
        await using var check = fixture.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var rows = await db.Challenges.Where(item => item.StartsAt == DateTimeOffset.Parse("2028-01-03T00:00:00Z")).ToArrayAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(item => item.Target == 3 && item.EndsAt == DateTimeOffset.Parse("2028-01-10T00:00:00Z"));
        rows.Select(item => item.RewardXp).Should().BeEquivalentTo(new[] { 20, 30 });
    }

    [Fact]
    public async Task ThirdNewLessonRewardsOnceAndReadCannotCreateProgress()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var token = await user.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token)).EnsureSuccessStatusCode();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await scope.ServiceProvider.GetRequiredService<WeeklyChallengeCatalog>().EnsureCurrentWeekAsync(CancellationToken.None);
            for (int index = 0; index < 2; index++)
                db.XpTransactions.Add(new XpTransaction
                {
                    UserId = user.Id,
                    CourseId = content.CourseId,
                    SourceType = "FirstLessonCompletion",
                    SourceId = Guid.NewGuid(),
                    Amount = 20,
                    EarnedAt = DateTimeOffset.UtcNow.AddSeconds(-5)
                });
            await db.SaveChangesAsync();
        }
        var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/challenges", bearer: token);
        var before = await Read<ChallengeView[]>(response);
        before.Should().OnlyContain(item => item.RewardedAt == null);
        before.Single(item => item.RuleType == "CompletedLessons" && item.IsActive).Progress.Should().Be(2);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await fixture.Client.GetAsync("/api/v1/me/challenges")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var session = await AnswerAsync(browser, token, content.LessonId);
        var results = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token),
            browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token));
        foreach (var result in results) (await Read<SessionView>(result)).AwardedXp.Should().Be(20);
        var list = await Read<ChallengeView[]>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/challenges", bearer: token));
        var earned = list.Single(item => item.RuleType == "CompletedLessons" && item.EndsAt > DateTimeOffset.UtcNow && item.StartsAt < DateTimeOffset.UtcNow);
        earned.Progress.Should().Be(3);
        earned.RewardedAt.Should().NotBeNull();
        earned.RewardCourseId.Should().Be(content.CourseId);
        var repeat = await AnswerAsync(browser, token, content.LessonId);
        (await Read<SessionView>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{repeat.Id}/complete", bearer: token))).AwardedXp.Should().Be(0);

        await using var check = fixture.Services.CreateAsyncScope();
        var database = check.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.XpTransactions.Where(item => item.UserId == user.Id).SumAsync(item => item.Amount)).Should().Be(80);
        (await database.Notifications.CountAsync(item => item.UserId == user.Id && item.Type == "Challenge")).Should().Be(1);
        var activity = await database.DailyActivities.SingleAsync(item => item.UserId == user.Id);
        activity.EarnedXp.Should().Be(40);
        activity.CompletedLessons.Should().Be(1);
        activity.GoalReached.Should().BeTrue();
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"UserChallenges\" (\"UserId\", \"ChallengeId\", \"Progress\") VALUES ({user.Id}, {earned.Id}, 0)"));
        duplicate.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        using var otherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var other = await SecurityTestAccount.CreateAsync(fixture);
        var otherToken = await other.LoginAsync(otherBrowser);
        var others = await Read<ChallengeView[]>(await otherBrowser.SendAsync(HttpMethod.Get, "/api/v1/me/challenges", bearer: otherToken));
        others.Should().OnlyContain(item => item.Progress == 0 && item.RewardedAt == null && item.RewardCourseId == null);
    }

    [Fact]
    public async Task ThreeUtcStudyDaysRewardTheCompletingLanguageWithoutRewardRecursion()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var token = await user.LoginAsync(browser);
        var spanish = await LearningTestContent.CreateAsync(fixture, user.Id);
        var italian = await LearningTestContent.CreateAsync(fixture, user.Id, courseNumber: 2);
        foreach (var courseId in new[] { spanish.CourseId, italian.CourseId })
            (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId }, token)).EnsureSuccessStatusCode();
        var challenge = new Challenge
        {
            Title = "Test study days",
            RuleType = "StudyDays",
            Target = 3,
            RewardXp = 30,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-4),
            EndsAt = DateTimeOffset.UtcNow.AddDays(1)
        };
        customChallenges.Add(challenge.Id);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.Challenges.Add(challenge);
            foreach (int day in new[] { -2, -1 })
                db.XpTransactions.Add(new XpTransaction
                {
                    UserId = user.Id,
                    CourseId = spanish.CourseId,
                    SourceType = "FirstLessonCompletion",
                    SourceId = Guid.NewGuid(),
                    Amount = 20,
                    EarnedAt = DateTimeOffset.UtcNow.AddDays(day)
                });
            db.XpTransactions.Add(new XpTransaction
            {
                UserId = user.Id,
                CourseId = spanish.CourseId,
                SourceType = "Challenge",
                SourceId = Guid.NewGuid(),
                Amount = 30,
                EarnedAt = DateTimeOffset.UtcNow.AddDays(-3)
            });
            await db.SaveChangesAsync();
        }
        var session = await AnswerAsync(browser, token, italian.LessonId);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token)).EnsureSuccessStatusCode();
        var list = await Read<ChallengeView[]>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/challenges", bearer: token));
        var earned = list.Single(item => item.Id == challenge.Id);
        earned.Progress.Should().Be(3);
        earned.RewardCourseId.Should().Be(italian.CourseId);
        await using var check = fixture.Services.CreateAsyncScope();
        var database = check.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.XpTransactions.SingleAsync(item => item.UserId == user.Id && item.SourceId == challenge.Id)).Amount.Should().Be(30);
    }

    [Fact]
    public async Task PracticeDoesNotAdvanceChallengesAndInvalidCatalogIsRejected()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var token = await user.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token)).EnsureSuccessStatusCode();
        var session = await AnswerAsync(browser, token, content.LessonId, "Practice");
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token)).EnsureSuccessStatusCode();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.UserChallenges.CountAsync(item => item.UserId == user.Id)).Should().Be(0);
        (await db.XpTransactions.CountAsync(item => item.UserId == user.Id)).Should().Be(0);
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"Challenges\" (\"Id\", \"Title\", \"RuleType\", \"Target\", \"RewardXp\", \"StartsAt\", \"EndsAt\") VALUES ({Guid.NewGuid()}, 'Invalid', 'CompletedLessons', 0, 20, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow.AddDays(1)})"));
        invalid.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }
    [Fact]
    public async Task ChallengeWindowIncludesStartButExcludesEndAndOtherRewardSources()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await user.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token)).EnsureSuccessStatusCode();
        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(1);
        var active = new Challenge { Title = "Boundary fixture", RuleType = "CompletedLessons", Target = 3, RewardXp = 20, StartsAt = start, EndsAt = end };
        var expired = new Challenge { Title = "Expired fixture", RuleType = "CompletedLessons", Target = 1, RewardXp = 20, StartsAt = start.AddDays(-2), EndsAt = start };
        customChallenges.AddRange([active.Id, expired.Id]);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.Challenges.AddRange(active, expired);
            foreach (var date in new[] { start.AddSeconds(-1), start, end })
                db.XpTransactions.Add(new XpTransaction
                {
                    UserId = user.Id,
                    CourseId = content.CourseId,
                    SourceType = "FirstLessonCompletion",
                    SourceId = Guid.NewGuid(),
                    Amount = 20,
                    EarnedAt = date
                });
            db.XpTransactions.Add(new XpTransaction
            {
                UserId = user.Id,
                CourseId = content.CourseId,
                SourceType = "Challenge",
                SourceId = Guid.NewGuid(),
                Amount = 20,
                EarnedAt = start.AddHours(1)
            });
            await db.SaveChangesAsync();
        }
        var session = await AnswerAsync(browser, token, content.LessonId);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token)).EnsureSuccessStatusCode();
        await using var check = fixture.Services.CreateAsyncScope();
        var database = check.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var state = await database.UserChallenges.SingleAsync(item => item.UserId == user.Id && item.ChallengeId == active.Id);
        state.Progress.Should().Be(2);
        state.RewardedAt.Should().BeNull();
        (await database.UserChallenges.AnyAsync(item => item.UserId == user.Id && item.ChallengeId == expired.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task RewardFailureRollsBackLessonProgressAndCanBeRetriedSafely()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await user.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token)).EnsureSuccessStatusCode();
        var challenge = new Challenge
        {
            Title = "Rollback fixture",
            RuleType = "CompletedLessons",
            Target = 1,
            RewardXp = 20,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndsAt = DateTimeOffset.UtcNow.AddDays(1)
        };
        customChallenges.Add(challenge.Id);
        Guid conflictingId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.Challenges.Add(challenge);
            var conflicting = new XpTransaction
            {
                UserId = user.Id,
                CourseId = content.CourseId,
                SourceType = "Challenge",
                SourceId = challenge.Id,
                Amount = 20,
                EarnedAt = DateTimeOffset.UtcNow
            };
            conflictingId = conflicting.Id;
            db.XpTransactions.Add(conflicting);
            await db.SaveChangesAsync();
        }
        var session = await AnswerAsync(browser, token, content.LessonId);
        var failed = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token);
        failed.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            (await db.LearningSessions.SingleAsync(item => item.Id == session.Id)).CompletedAt.Should().BeNull();
            (await db.XpTransactions.AnyAsync(item => item.UserId == user.Id && item.SourceType == "FirstLessonCompletion")).Should().BeFalse();
            (await db.UserChallenges.AnyAsync(item => item.UserId == user.Id)).Should().BeFalse();
            (await db.Notifications.AnyAsync(item => item.UserId == user.Id && item.Type == "Challenge")).Should().BeFalse();
            (await db.DailyActivities.AnyAsync(item => item.UserId == user.Id)).Should().BeFalse();
            // Remove only the deliberately inconsistent record in this disposable test database.
            await db.XpTransactions.Where(item => item.Id == conflictingId).ExecuteDeleteAsync();
        }
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token)).EnsureSuccessStatusCode();
        await using var check = fixture.Services.CreateAsyncScope();
        var database = check.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.UserChallenges.SingleAsync(item => item.UserId == user.Id && item.ChallengeId == challenge.Id)).RewardedAt.Should().NotBeNull();
        (await database.XpTransactions.CountAsync(item => item.UserId == user.Id && item.SourceId == challenge.Id)).Should().Be(1);
    }

}

