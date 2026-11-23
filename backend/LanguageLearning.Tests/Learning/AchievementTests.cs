using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Vocabulary;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class AchievementTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private async Task<(SecurityTestAccount User, string Token, LearningTestContent Content)> SetupAsync(SessionTestBrowser browser)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        string token = await user.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token)).EnsureSuccessStatusCode();
        return (user, token, content);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static Task<HttpResponseMessage> Collect(SessionTestBrowser browser, string token) =>
        browser.SendAsync(HttpMethod.Post, "/api/v1/me/achievements/collect", new { }, token);
    private static async Task<AchievementView[]> List(SessionTestBrowser browser, string token) =>
        await Read<AchievementView[]>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/achievements", bearer: token));
    private async Task<SessionView> AnswerAll(SessionTestBrowser browser, string token, SessionView session)
    {
        while (session.CurrentExercise is { } exercise)
        {
            (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
                new { sessionExerciseId = exercise.Id, answerText = "Hola", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
            session = await Read<SessionView>(await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: token));
        }
        return session;
    }

    [Fact]
    public async Task FreshCatalogIsReadOnlyAndClientCannotGrantBadges()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(browser);
        var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/achievements", bearer: state.Token);
        var values = await Read<AchievementView[]>(response);
        values.Should().HaveCount(4);
        values.Should().OnlyContain(item => item.EarnedAt == null && !item.CanCollect && item.Progress == 0);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await Read<AchievementView[]>(await Collect(browser, state.Token))).Should().BeEmpty();
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/achievements/collect", new { userId = Guid.NewGuid(), progress = 100 }, state.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fixture.Client.GetAsync("/api/v1/me/achievements")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fixture.Client.PostAsJsonAsync("/api/v1/me/achievements/collect", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.UserAchievements.CountAsync(item => item.UserId == state.User.Id)).Should().Be(0);
        (await db.Notifications.CountAsync(item => item.UserId == state.User.Id && item.Type == "Achievement")).Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentPerfectCompletionAwardsAndNotifiesExactlyOnceWithoutExtraXp()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(browser);
        var session = await Read<SessionView>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{state.Content.LessonId}/sessions", bearer: state.Token));
        await AnswerAll(browser, state.Token, session);
        var completed = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: state.Token),
            browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: state.Token));
        foreach (var item in completed) (await Read<SessionView>(item)).AwardedXp.Should().Be(20);
        var badges = await List(browser, state.Token);
        badges.Where(item => item.EarnedAt != null).Select(item => item.Code).Should().BeEquivalentTo("first-lesson", "perfect-lesson");
        (await Read<AchievementView[]>(await Collect(browser, state.Token))).Should().BeEmpty();
        using var other = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var outsider = await SecurityTestAccount.CreateAsync(fixture);
        string outsiderToken = await outsider.LoginAsync(other);
        (await List(other, outsiderToken)).Should().OnlyContain(item => item.EarnedAt == null);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.Notifications.CountAsync(item => item.UserId == state.User.Id && item.Type == "Achievement")).Should().Be(2);
        (await db.XpTransactions.Where(item => item.UserId == state.User.Id).SumAsync(item => item.Amount)).Should().Be(20);
        var award = await db.UserAchievements.FirstAsync(item => item.UserId == state.User.Id);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"UserAchievements\" (\"UserId\", \"AchievementId\", \"EarnedAt\") VALUES ({award.UserId}, {award.AchievementId}, {DateTimeOffset.UtcNow})"));
        duplicate.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"UserAchievements\" (\"UserId\", \"AchievementId\", \"EarnedAt\") VALUES ({state.User.Id}, {Guid.NewGuid()}, {DateTimeOffset.UtcNow})"));
        invalid.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task HistoricalStreakCanBeCollectedOnceAndFutureDatesDoNotQualify()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(browser);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            foreach (int offset in new[] { -9, -8, -7, 1, 2, 3, 4, 5 })
                db.DailyActivities.Add(new DailyActivity { UserId = state.User.Id, LocalDate = today.AddDays(offset), EarnedXp = 20, CompletedLessons = 1, GoalReached = true });
            await db.SaveChangesAsync();
        }
        var ready = (await List(browser, state.Token)).Single(item => item.Code == "goal-streak");
        ready.Progress.Should().Be(3);
        ready.CanCollect.Should().BeTrue();
        ready.EarnedAt.Should().BeNull();
        var results = await Task.WhenAll(Collect(browser, state.Token), Collect(browser, state.Token));
        var awarded = new List<AchievementView>();
        foreach (var response in results) awarded.AddRange(await Read<AchievementView[]>(response));
        awarded.Should().ContainSingle().Which.Code.Should().Be("goal-streak");
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var final = finalScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await final.UserAchievements.CountAsync(item => item.UserId == state.User.Id)).Should().Be(1);
        (await final.Notifications.CountAsync(item => item.UserId == state.User.Id && item.Type == "Achievement")).Should().Be(1);
        (await final.XpTransactions.CountAsync(item => item.UserId == state.User.Id)).Should().Be(0);
    }

    [Fact]
    public async Task SavedWordMilestoneIsAutomaticAndSurvivesRemovingAWord()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(browser);
        var words = Enumerable.Range(1, 10).Select(index => new VocabularyItem
        { CourseId = state.Content.CourseId, Word = $"badge-{Guid.NewGuid():N}-{index}", EnglishTranslation = "Test word", Example = "Test example", PartOfSpeech = "noun" }).ToArray();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.VocabularyItems.AddRange(words);
            await db.SaveChangesAsync();
        }
        Guid firstSaved = Guid.Empty;
        for (int index = 0; index < 9; index++)
        {
            var saved = await Read<SaveWordResult>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = words[index].Id }, state.Token));
            if (index == 0) firstSaved = saved.Id;
        }
        (await List(browser, state.Token)).Single(item => item.Code == "word-collector").EarnedAt.Should().BeNull();
        foreach (var response in await Task.WhenAll(
            browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = words[9].Id }, state.Token),
            browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = words[9].Id }, state.Token))) response.EnsureSuccessStatusCode();
        var earned = (await List(browser, state.Token)).Single(item => item.Code == "word-collector");
        earned.EarnedAt.Should().NotBeNull();
        (await browser.SendAsync(HttpMethod.Delete, "/api/v1/me/saved-words/" + firstSaved, bearer: state.Token)).EnsureSuccessStatusCode();
        var retained = (await List(browser, state.Token)).Single(item => item.Code == "word-collector");
        retained.Progress.Should().Be(9);
        retained.EarnedAt.Should().Be(earned.EarnedAt);
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var dbFinal = finalScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await dbFinal.Notifications.CountAsync(item => item.UserId == state.User.Id && item.Type == "Achievement")).Should().Be(1);
        (await dbFinal.XpTransactions.CountAsync(item => item.UserId == state.User.Id)).Should().Be(0);
    }

    [Fact]
    public async Task PerfectPracticeDoesNotBecomeAPerfectLessonBadge()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(browser);
        var session = await Read<SessionView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", new { lessonId = state.Content.LessonId }, state.Token));
        await AnswerAll(browser, state.Token, session);
        (await Read<SessionView>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: state.Token))).Score.Should().Be(100);
        (await List(browser, state.Token)).Should().OnlyContain(item => !item.CanCollect && item.EarnedAt == null);
    }
}
