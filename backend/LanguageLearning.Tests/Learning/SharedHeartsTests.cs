using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class SharedHeartsTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task HeartsAreSharedExhaustionBlocksFreshLessonsAndServerRefillResumesTheSavedSession()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(browser);
        var first = await LearningTestContent.CreateAsync(fixture, user.Id, count: 6);
        var second = await LearningTestContent.CreateAsync(fixture, user.Id, count: 2);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { first.CourseId }, token)).EnsureSuccessStatusCode();
        async Task<SessionView> Start(Guid lessonId) => (await (await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lessonId}/sessions", bearer: token)).Content.ReadFromJsonAsync<SessionView>())!;
        async Task<SessionView> Read(Guid sessionId) => (await (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{sessionId}", bearer: token)).Content.ReadFromJsonAsync<SessionView>())!;
        var session = await Start(first.LessonId);
        var another = await Start(second.LessonId);
        Guid key = Guid.Empty; Guid exerciseId = Guid.Empty;
        for (int index = 0; index < 5; index++)
        {
            key = Guid.NewGuid(); exerciseId = session.CurrentExercise!.Id;
            (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = exerciseId, answerText = "wrong", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = key }, token)).EnsureSuccessStatusCode();
            session = await Read(session.Id);
            Assert.Equal(4 - index, session.Hearts);
        }
        var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/hearts", bearer: token);
        var status = (await response.Content.ReadFromJsonAsync<HeartStatus>())!;
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(0, status.Count); Assert.Equal(5, status.Maximum);
        Assert.InRange((status.RefillAt!.Value - status.ServerNow).TotalSeconds, 3590, 3600);
        Assert.Equal(0, (await Read(another.Id)).Hearts);
        Assert.Null((await Read(another.Id)).CurrentExercise);
        // Canonical replay cannot charge again or move the timer.
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = exerciseId, answerText = "wrong", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = key }, token)).EnsureSuccessStatusCode();
        var afterReplay = (await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/hearts", bearer: token)).Content.ReadFromJsonAsync<HeartStatus>())!;
        Assert.Equal(status.RefillAt, afterReplay.RefillAt);
        var third = await LearningTestContent.CreateAsync(fixture, user.Id, count: 1);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{third.LessonId}/sessions", bearer: token)).StatusCode);
        var practice = (await (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", new { lessonId = first.LessonId }, token)).Content.ReadFromJsonAsync<SessionView>())!;
        Assert.Equal(5, practice.Hearts);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.LearnerHearts.Where(item => item.UserId == user.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RefillAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        var recovered = await Read(session.Id);
        Assert.Equal(5, recovered.Hearts); Assert.Equal(session.Id, recovered.Id); Assert.Equal(5, recovered.AnsweredCount); Assert.NotNull(recovered.CurrentExercise);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{recovered.Id}/answers", new { sessionExerciseId = recovered.CurrentExercise!.Id, answerText = "Hola", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
        var persisted = await db.LearnerHearts.AsNoTracking().SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(5, persisted.Count); Assert.Null(persisted.RefillAt);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/v1/me/hearts")).StatusCode);
    }

    [Fact]
    public async Task ParallelAnswersInDifferentLessonsCannotOverspendOrLoseAnUpdate()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(browser);
        var first = await LearningTestContent.CreateAsync(fixture, user.Id, count: 2);
        var second = await LearningTestContent.CreateAsync(fixture, user.Id, count: 2);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { first.CourseId }, token)).EnsureSuccessStatusCode();
        async Task<SessionView> Start(Guid id) => (await (await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{id}/sessions", bearer: token)).Content.ReadFromJsonAsync<SessionView>())!;
        var a = await Start(first.LessonId); var b = await Start(second.LessonId);
        var responses = await Task.WhenAll(new[] { a, b }.Select(session => browser.SendAsync(HttpMethod.Post,
            $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise!.Id, answerText = "wrong", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, token)));
        Assert.All(responses, response => response.EnsureSuccessStatusCode());
        var feedback = await Task.WhenAll(responses.Select(async response => (await response.Content.ReadFromJsonAsync<AnswerFeedback>())!));
        Assert.Equal(new[] { 3, 4 }, feedback.Select(item => item.HeartsRemaining).Order().ToArray());
        var status = (await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/hearts", bearer: token)).Content.ReadFromJsonAsync<HeartStatus>())!;
        Assert.Equal(3, status.Count); Assert.Null(status.RefillAt);
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var other = await SecurityTestAccount.CreateAsync(fixture); string otherToken = await other.LoginAsync(outsider);
        var otherStatus = (await (await outsider.SendAsync(HttpMethod.Get, "/api/v1/me/hearts?userId=" + user.Id, bearer: otherToken)).Content.ReadFromJsonAsync<HeartStatus>())!;
        Assert.Equal(5, otherStatus.Count);
    }

    [Fact]
    public async Task SqlRejectsInvalidBalancesAndMissingOwners()
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"LearnerHearts\" (\"UserId\",\"Count\",\"RefillAt\") VALUES ({user.Id},0,NULL)"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        var missing = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"LearnerHearts\" (\"UserId\",\"Count\",\"RefillAt\") VALUES ({Guid.NewGuid()},5,NULL)"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, missing.SqlState);
    }
}
