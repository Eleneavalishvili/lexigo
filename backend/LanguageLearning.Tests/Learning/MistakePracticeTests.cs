using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class MistakePracticeTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task ReviewUsesOwnedSavedAnswersAndClearsOnlyAfterSuccessfulCompletedPractice()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        string token = await learner.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, learner.Id, 6);
        await EnrollAsync(browser, token, content.CourseId);
        var lesson = await SessionAsync(await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: token));
        for (int index = 0; index < 6; index++) lesson = await AnswerAsync(browser, token, lesson, index != 0);
        var original = await CompleteAsync(browser, token, lesson.Id);
        Assert.Equal(20, original.AwardedXp);
        var mistakes = await ListAsync(browser, token, content.CourseId);
        var skillRoute = $"/api/v1/me/courses/{content.CourseId}/skills";
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, skillRoute)).StatusCode);
        var skillResponse = await browser.SendAsync(HttpMethod.Get, skillRoute, bearer: token);
        skillResponse.EnsureSuccessStatusCode();
        Assert.True(skillResponse.Headers.CacheControl!.NoStore);
        var map = (await skillResponse.Content.ReadFromJsonAsync<SkillMapEntry[]>())!;
        Assert.Contains(map, skill => skill.Category == "vocabulary" && skill.CorrectCount == 5
            && skill.IncorrectCount == 1 && skill.LastPracticedAt.HasValue);
        Assert.Contains(map, skill => skill.Category == "grammar" && skill.LastPracticedAt is null);
        Assert.Single(mistakes.Items);
        Assert.Equal(1, mistakes.Items[0].WrongCount);
        Assert.Equal(new[] { "Vocabulary" }, mistakes.Items[0].Skills);
        Assert.Equal(mistakes.Items[0].LastMissedAt, mistakes.Items[0].LastAttemptAt);

        var outsider = await SecurityTestAccount.CreateAsync(fixture);
        using var other = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string otherToken = await outsider.LoginAsync(other);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.SendAsync(HttpMethod.Get, skillRoute, bearer: otherToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.SendAsync(HttpMethod.Get, $"/api/v1/me/mistakes?courseId={content.CourseId}", bearer: otherToken)).StatusCode);
        await EnrollAsync(other, otherToken, content.CourseId);
        var otherMap = await other.SendAsync(HttpMethod.Get, skillRoute, bearer: otherToken);
        otherMap.EnsureSuccessStatusCode();
        Assert.All((await otherMap.Content.ReadFromJsonAsync<SkillMapEntry[]>())!,
            skill => Assert.Null(skill.LastPracticedAt));
        Assert.Empty((await ListAsync(other, otherToken, content.CourseId)).Items);
        Assert.Equal(HttpStatusCode.Conflict, (await StartReviewAsync(other, otherToken, content.CourseId)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/mistakes?courseId={content.CourseId}&skillId={Guid.NewGuid()}", bearer: token)).StatusCode);

        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var grammar = await database.Skills.SingleAsync(item => item.CourseId == content.CourseId && item.Category == "grammar");
        database.ExerciseSkills.Add(new ExerciseSkill { ExerciseId = mistakes.Items[0].ExerciseId, SkillId = grammar.Id });
        await database.SaveChangesAsync();
        int vocabularyId = map.Single(item => item.Category == "vocabulary").Id;
        var filtered = await browser.SendAsync(HttpMethod.Get,
            $"/api/v1/me/mistakes?courseId={content.CourseId}&skillId={vocabularyId}", bearer: token);
        filtered.EnsureSuccessStatusCode();
        Assert.Equal(mistakes.Items[0].ExerciseId, Assert.Single((await filtered.Content.ReadFromJsonAsync<MistakePage>())!.Items).ExerciseId);
        var grammarMistakes = await browser.SendAsync(HttpMethod.Get,
            $"/api/v1/me/mistakes?courseId={content.CourseId}&skillId={grammar.Id}", bearer: token);
        grammarMistakes.EnsureSuccessStatusCode();
        Assert.Empty((await grammarMistakes.Content.ReadFromJsonAsync<MistakePage>())!.Items);
        await database.AcceptedAnswers.Where(item => content.ExerciseIds.Contains(item.ExerciseId))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.AnswerText, "Edited after the original session"));
        var starts = await Task.WhenAll(StartReviewAsync(browser, token, content.CourseId), StartReviewAsync(browser, token, content.CourseId));
        using var created = Assert.Single(starts, response => response.StatusCode == HttpStatusCode.Created);
        using var resumed = Assert.Single(starts, response => response.StatusCode == HttpStatusCode.OK);
        var practice = await SessionAsync(created);
        Assert.Equal(practice.Id, (await SessionAsync(resumed)).Id);
        Assert.NotNull(created.Headers.Location);
        var detailPath = created.Headers.Location.AbsolutePath;
        Assert.Equal($"/api/v1/me/learning-sessions/{practice.Id}", detailPath);
        using var detail = await browser.SendAsync(HttpMethod.Get, detailPath, bearer: token);
        Assert.Equal(practice.Id, (await SessionAsync(detail)).Id);
        var targetedStarts = await Task.WhenAll(
            StartReviewAsync(browser, token, content.CourseId, vocabularyId),
            StartReviewAsync(browser, token, content.CourseId, vocabularyId));
        var targeted = await SessionAsync(targetedStarts[0]);
        Assert.Equal(targeted.Id, (await SessionAsync(targetedStarts[1])).Id);
        Assert.Equal(vocabularyId, targeted.PracticeSkillId);
        Assert.NotEqual(practice.Id, targeted.Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await StartReviewAsync(browser, token, content.CourseId, int.MaxValue)).StatusCode);
        Assert.Equal("Practice", practice.Mode);
        Assert.Null(practice.LessonId);
        Assert.NotNull(practice.CurrentExercise);
        Assert.Equal(1, practice.TotalCount);
        Assert.DoesNotContain("privateEvaluationSnapshot", await starts[0].Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Edited after", await starts[0].Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{practice.Id}", bearer: otherToken)).StatusCode);
        await AnswerAsync(browser, token, practice, false);
        Assert.Single((await ListAsync(browser, token, content.CourseId)).Items);
        Assert.Equal(0, (await CompleteAsync(browser, token, practice.Id)).AwardedXp);
        Assert.Equal(2, (await ListAsync(browser, token, content.CourseId)).Items[0].WrongCount);
        practice = await SessionAsync(await StartReviewAsync(browser, token, content.CourseId));
        await AnswerAsync(browser, token, practice, true);
        Assert.Single((await ListAsync(browser, token, content.CourseId)).Items);
        var completed = await CompleteAsync(browser, token, practice.Id);
        Assert.True(completed.Passed);
        Assert.Equal(0, completed.AwardedXp);
        Assert.Empty((await ListAsync(browser, token, content.CourseId)).Items);
        Assert.Equal(20, await database.XpTransactions.Where(item => item.UserId == learner.Id).SumAsync(item => item.Amount));
        var skillProgress = await database.SkillProgress.SingleAsync(item => item.UserId == learner.Id);
        Assert.Equal("vocabulary", (await database.Skills.SingleAsync(item => item.Id == skillProgress.SkillId)).Category);
        Assert.Equal(6, skillProgress.CorrectCount);
        Assert.Equal(2, skillProgress.IncorrectCount);
        Assert.NotNull(skillProgress.LastPracticedAt);
        Assert.Equal(1, await database.LessonProgress.CountAsync(item => item.UserId == learner.Id));
        var originalAgain = await SessionAsync(await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{original.Id}", bearer: token));
        Assert.Equal(original.Score, originalAgain.Score);
        Assert.Equal(original.AwardedXp, originalAgain.AwardedXp);
        var hearts = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/hearts", bearer: token);
        hearts.EnsureSuccessStatusCode();
        using var heartJson = System.Text.Json.JsonDocument.Parse(await hearts.Content.ReadAsStringAsync());
        Assert.Equal(4, heartJson.RootElement.GetProperty("count").GetInt32());

        // Simulate history saved before session skill IDs and before all counters were collected.
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "SessionExercises"
            SET "PrivateEvaluationSnapshot" = "PrivateEvaluationSnapshot" - 'skillIds'
            WHERE "SessionId" = {original.Id}
            """);
        await database.SkillProgress.Where(item => item.UserId == learner.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.CorrectCount, 1));
        var migrator = database.GetService<IMigrator>();
        var beforeBackfill = (await database.Database.GetAppliedMigrationsAsync())
            .Single(id => id.EndsWith("_AddSkillPractice", StringComparison.Ordinal));
        for (int run = 0; run < 2; run++)
        {
            await migrator.MigrateAsync(beforeBackfill);
            await migrator.MigrateAsync();
            var rebuilt = await database.SkillProgress.AsNoTracking().SingleAsync(item => item.UserId == learner.Id);
            Assert.Equal(skillProgress.SkillId, rebuilt.SkillId);
            Assert.Equal(6, rebuilt.CorrectCount);
            Assert.Equal(2, rebuilt.IncorrectCount);
            Assert.Equal(skillProgress.LastPracticedAt, rebuilt.LastPracticedAt);
        }

        var dueRoute = $"/api/v1/me/mistakes?courseId={content.CourseId}&includeDue=true";
        var recent = await browser.SendAsync(HttpMethod.Get, dueRoute, bearer: token);
        recent.EnsureSuccessStatusCode();
        Assert.Empty((await recent.Content.ReadFromJsonAsync<MistakePage>())!.Items);
        await database.AnswerAttempts.Where(item => item.UserId == learner.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.SubmittedAt, item => item.SubmittedAt.AddDays(-4)));
        var older = await browser.SendAsync(HttpMethod.Get, dueRoute, bearer: token);
        older.EnsureSuccessStatusCode();
        var due = (await older.Content.ReadFromJsonAsync<MistakePage>())!.Items;
        Assert.Equal(6, due.Length);
        Assert.All(due, item => Assert.NotNull(item.DueAt));
        Assert.Contains(due, item => item.LastMissedAt is null);
        Assert.Empty((await ListAsync(browser, token, content.CourseId)).Items);
        Assert.Equal(6, (await SessionAsync(await StartReviewAsync(browser, token, content.CourseId))).TotalCount);
    }

    [Fact]
    public async Task CorrectChoiceReviewDoesNotCompleteTheFailedLessonOrUnlockItsDependent()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        string token = await learner.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, learner.Id, 1, "MultipleChoice");
        var dependent = await LearningTestContent.CreateAsync(fixture, learner.Id, 1);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        database.LessonPrerequisites.Add(new LessonPrerequisite { LessonId = dependent.LessonId, PrerequisiteLessonId = content.LessonId });
        await database.SaveChangesAsync();
        await EnrollAsync(browser, token, content.CourseId);
        var lesson = await SessionAsync(await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: token));
        await AnswerAsync(browser, token, lesson, false);
        await CompleteAsync(browser, token, lesson.Id);
        var practice = await SessionAsync(await StartReviewAsync(browser, token, content.CourseId));
        await AnswerAsync(browser, token, practice, true);
        Assert.Equal(0, (await CompleteAsync(browser, token, practice.Id)).AwardedXp);
        Assert.Empty((await ListAsync(browser, token, content.CourseId)).Items);
        Assert.False(await database.LessonProgress.AnyAsync(item => item.UserId == learner.Id && item.Status == "Completed"));
        Assert.False(await database.XpTransactions.AnyAsync(item => item.UserId == learner.Id));
        Assert.Equal(HttpStatusCode.Conflict,
            (await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{dependent.LessonId}/sessions", bearer: token)).StatusCode);
    }

    [Fact]
    public async Task ListingIsPagedAndPracticeIsBoundedWithoutDiscardingOlderMistakes()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        string token = await learner.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, learner.Id, 30);
        await EnrollAsync(browser, token, content.CourseId);
        var session = await SessionAsync(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", new { content.LessonId }, token));
        while (session.CurrentExercise is not null) session = await AnswerAsync(browser, token, session, false);
        await CompleteAsync(browser, token, session.Id);
        var first = await ListAsync(browser, token, content.CourseId);
        var second = await ListAsync(browser, token, content.CourseId, 2);
        var empty = await ListAsync(browser, token, content.CourseId, 100);
        Assert.Equal(30, first.TotalCount);
        Assert.Equal(20, first.Items.Length);
        Assert.Equal(10, second.Items.Length);
        Assert.Equal(30, empty.TotalCount);
        Assert.Empty(empty.Items);
        Assert.Empty(first.Items.Select(item => item.ExerciseId).Intersect(second.Items.Select(item => item.ExerciseId)));

        var choiceLesson = await LearningTestContent.CreateAsync(fixture, learner.Id, 1, "MultipleChoice");
        Assert.Equal(content.CourseId, choiceLesson.CourseId);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var grammar = await db.Skills.SingleAsync(item => item.CourseId == content.CourseId && item.Category == "grammar");
        var harderUnit = await db.Lessons.Where(item => item.Id == choiceLesson.LessonId).Select(item => item.UnitId).SingleAsync();
        await db.Units.Where(item => item.Id == harderUnit)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Difficulty, "B2"));
        db.ExerciseSkills.Add(new ExerciseSkill { ExerciseId = choiceLesson.ExerciseIds.Single(), SkillId = grammar.Id });
        await db.SaveChangesAsync();
        var choice = await SessionAsync(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions",
            new { choiceLesson.LessonId }, token));
        await AnswerAsync(browser, token, choice, false);
        await CompleteAsync(browser, token, choice.Id);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AnswerAttempts" attempt
            SET "SubmittedAt" = attempt."SubmittedAt" - INTERVAL '1 day'
            FROM "SessionExercises" exercise
            WHERE exercise."SessionId" = {choice.Id} AND exercise."Id" = attempt."SessionExerciseId"
            """);
        Assert.DoesNotContain((await ListAsync(browser, token, content.CourseId)).Items.Take(10),
            item => item.Type == "MultipleChoice");

        var practice = await SessionAsync(await StartReviewAsync(browser, token, content.CourseId));
        Assert.Equal(10, practice.TotalCount);
        for (int index = 0; index < 2; index++)
        {
            Assert.Equal("Translation", practice.CurrentExercise!.Type);
            practice = await AnswerAsync(browser, token, practice, true);
        }
        Assert.Equal("MultipleChoice", practice.CurrentExercise!.Type);
        await db.SkillProgress.Where(item => item.UserId == learner.Id && item.SkillId != grammar.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.CorrectCount, 31));
        Assert.Equal(choiceLesson.ExerciseIds.Single(), (await ListAsync(browser, token, content.CourseId)).Items[0].ExerciseId);
        await db.SkillProgress.Where(item => item.UserId == learner.Id && item.SkillId == grammar.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.CorrectCount, 1));
        Assert.NotEqual(choiceLesson.ExerciseIds.Single(), (await ListAsync(browser, token, content.CourseId)).Items[0].ExerciseId);
        var completedExercises = db.SessionExercises.Where(item => item.SessionId == session.Id);
        var recentIds = await db.AnswerAttempts.Where(item => item.UserId == learner.Id
                && completedExercises.Any(exercise => exercise.Id == item.SessionExerciseId))
            .OrderByDescending(item => item.SubmittedAt).ThenByDescending(item => item.Id)
            .Select(item => item.Id).Take(8).ToArrayAsync();
        await db.AnswerAttempts.Where(item => recentIds.Contains(item.Id))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsCorrect, true));
        Assert.Equal(choiceLesson.ExerciseIds.Single(), (await ListAsync(browser, token, content.CourseId)).Items[0].ExerciseId);
    }

    private static async Task EnrollAsync(SessionTestBrowser browser, string token, Guid courseId) =>
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId }, token)).EnsureSuccessStatusCode();

    private static Task<HttpResponseMessage> StartReviewAsync(SessionTestBrowser browser, string token, Guid courseId, int? skillId = null) =>
        browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", new { courseId, mistakesOnly = true, skillId }, token);

    private static async Task<MistakePage> ListAsync(SessionTestBrowser browser, string token, Guid courseId, int page = 1)
    {
        var response = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/mistakes?courseId={courseId}&page={page}", bearer: token);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20, payload.GetProperty("pageSize").GetInt32());
        return payload.Deserialize<MistakePage>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static async Task<SessionView> SessionAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionView>())!;
    }

    private static async Task<SessionView> CompleteAsync(SessionTestBrowser browser, string token, Guid sessionId) =>
        await SessionAsync(await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{sessionId}/complete", bearer: token));

    private static async Task<SessionView> AnswerAsync(SessionTestBrowser browser, string token, SessionView session, bool correct)
    {
        var exercise = session.CurrentExercise!;
        Guid[] choices = exercise.Options.Length == 0 ? [] : [exercise.Options.First(item => (item.Text == "Hola") == correct).Id];
        var response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new
            {
                sessionExerciseId = exercise.Id,
                answerText = choices.Length == 0 ? (correct ? "Hola" : "wrong") : null,
                selectedOptionIds = choices,
                idempotencyKey = Guid.NewGuid()
            }, token);
        response.EnsureSuccessStatusCode();
        return await SessionAsync(await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: token));
    }
}
