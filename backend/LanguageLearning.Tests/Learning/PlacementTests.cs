using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class PlacementTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Theory]
    [InlineData("es", 2)]
    [InlineData("it", 2)]
    [InlineData("de", 0)]
    [InlineData("ru", -1)]
    public async Task ServerRecommendationIsOwnedPersistentAndNeverAwardsSkippedXp(string code, int failedUnit)
    {
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>().InstallAsync(CancellationToken.None);
        Guid courseId = StarterLessonInstaller.Read(code).CourseId;
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await learner.LoginAsync(browser);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId }, token)).EnsureSuccessStatusCode();
        string startRoute = $"/api/v1/courses/{courseId}/placement-sessions";
        var starts = await Task.WhenAll(browser.SendAsync(HttpMethod.Post, startRoute, bearer: token),
            browser.SendAsync(HttpMethod.Post, startRoute, bearer: token));
        using var created = Assert.Single(starts, response => response.StatusCode == HttpStatusCode.Created);
        using var resumed = Assert.Single(starts, response => response.StatusCode == HttpStatusCode.OK);
        var session = (await created.Content.ReadFromJsonAsync<SessionView>())!;
        Assert.Equal(session.Id, (await resumed.Content.ReadFromJsonAsync<SessionView>())!.Id);
        Assert.True(session.IsPlacement);
        int diagnosticUnits = await database.Units.CountAsync(item => item.CourseId == courseId && item.Title != "Cyrillic basics");
        Assert.Equal(diagnosticUnits * 5, session.TotalCount);
        Assert.NotNull(created.Headers.Location);
        var detailPath = created.Headers.Location.AbsolutePath;
        Assert.Equal($"/api/v1/me/learning-sessions/{session.Id}", detailPath);
        using var detail = await browser.SendAsync(HttpMethod.Get, detailPath, bearer: token);
        detail.EnsureSuccessStatusCode();
        string snapshot = await detail.Content.ReadAsStringAsync();
        Assert.DoesNotContain("placementMap", snapshot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("acceptedAnswers", snapshot, StringComparison.OrdinalIgnoreCase);
        string resultRoute = $"/api/v1/me/placement-sessions/{session.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, resultRoute + "/apply", bearer: token)).StatusCode);
        var outsider = await SecurityTestAccount.CreateAsync(fixture);
        using var other = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string otherToken = await outsider.LoginAsync(other);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Get, detailPath, bearer: otherToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Get, resultRoute, bearer: otherToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Post, resultRoute + "/apply", bearer: otherToken)).StatusCode);

        var stored = await database.LearningSessions.AsNoTracking().SingleAsync(item => item.Id == session.Id);
        using var mapping = JsonDocument.Parse(stored.PlacementMap!);
        var units = mapping.RootElement.EnumerateArray().ToArray();
        var failedIds = failedUnit < 0 ? [] : units[failedUnit].GetProperty("questionIds").EnumerateArray().Select(item => item.GetGuid()).ToArray();
        var snapshots = await database.SessionExercises.AsNoTracking().Where(item => item.SessionId == session.Id).ToDictionaryAsync(item => item.Id);
        while (session.CurrentExercise is { } exercise)
        {
            var keys = JsonSerializer.Deserialize<EvaluationSnapshot>(snapshots[exercise.Id].PrivateEvaluationSnapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            bool fail = failedIds.Contains(exercise.Id);
            Guid[] choices = exercise.Options.Length == 0 ? [] : [exercise.Options.First(item => keys.CorrectOptionIds.Contains(item.Id) != fail).Id];
            var answer = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
                new
                {
                    sessionExerciseId = exercise.Id,
                    answerText = choices.Length == 0 ? (fail ? "incorrect" : keys.Answers[0]) : null,
                    selectedOptionIds = choices,
                    idempotencyKey = Guid.NewGuid()
                }, token);
            answer.EnsureSuccessStatusCode();
            Assert.Equal(5, (await answer.Content.ReadFromJsonAsync<AnswerFeedback>())!.HeartsRemaining);
            var reload = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: token);
            reload.EnsureSuccessStatusCode(); session = (await reload.Content.ReadFromJsonAsync<SessionView>())!;
        }
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token)).EnsureSuccessStatusCode();
        var response = await browser.SendAsync(HttpMethod.Get, resultRoute, bearer: token);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<PlacementResult>())!;
        Guid expected = units[failedUnit < 0 ? units.Length - 1 : failedUnit].GetProperty("id").GetGuid();
        Assert.Equal(expected, result.RecommendedUnitId);
        Assert.True(result.Completed); Assert.False(result.Applied);
        var saved = await browser.SendAsync(HttpMethod.Post, resultRoute + "/apply", bearer: token);
        saved.EnsureSuccessStatusCode(); Assert.True((await saved.Content.ReadFromJsonAsync<PlacementResult>())!.Applied);
        (await browser.SendAsync(HttpMethod.Post, resultRoute + "/apply", bearer: token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, startRoute, bearer: token)).StatusCode);
        Assert.False(await database.XpTransactions.AnyAsync(item => item.UserId == learner.Id));
        Assert.False(await database.LessonProgress.AnyAsync(item => item.UserId == learner.Id));
        Assert.Equal(0, (await database.LearningSessions.AsNoTracking().SingleAsync(item => item.Id == session.Id)).AwardedXp);
        var progressResponse = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/progress?courseId={courseId}", bearer: token);
        progressResponse.EnsureSuccessStatusCode();
        var progress = (await progressResponse.Content.ReadFromJsonAsync<CourseProgress>())!;
        Assert.Equal(expected, progress.RecommendedUnitId);
        var targetLessons = progress.Lessons.Where(item => item.UnitId == expected).OrderBy(item => item.OrderNumber).ToArray();
        Assert.True(targetLessons[0].IsUnlocked);
        Assert.False(targetLessons[1].IsUnlocked);
        Assert.Equal(HttpStatusCode.Conflict,
            (await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{targetLessons[1].Id}/sessions", bearer: token)).StatusCode);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{targetLessons[0].Id}/sessions", bearer: token)).EnsureSuccessStatusCode();

        Guid foreignUnit = await database.Units.Where(item => item.CourseId != courseId).Select(item => item.Id).FirstAsync();
        await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LearningSessions\" SET \"RecommendedUnitId\"={foreignUnit} WHERE \"Id\"={session.Id}"));
        await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LearningSessions\" SET \"AwardedXp\"=20 WHERE \"Id\"={session.Id}"));
    }
}
