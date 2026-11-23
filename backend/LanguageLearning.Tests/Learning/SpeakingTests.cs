using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Storage;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class SpeakingTests(MediaWorkflowFixture fixture) : IClassFixture<MediaWorkflowFixture>
{
    [Fact]
    public async Task SpeakingOnlyCompletionIsPersistedUngradedWithoutHeartsXpOrAccuracyClaims()
    {
        using var browser = Browser();
        var state = await SetupAsync(browser, ["Speaking"]);
        Guid key = Guid.NewGuid();
        var response = await ConfirmAsync(browser, state.Token, state.Session, key);
        var feedback = (await response.Content.ReadFromJsonAsync<AnswerFeedback>())!;
        Assert.False(feedback.IsGraded);
        Assert.False(feedback.IsCorrect);
        Assert.Equal(5, feedback.HeartsRemaining);
        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(browser, state.Token, state.Session, key)).StatusCode);
        var completed = await CompleteAsync(browser, state.Token, state.Session.Id);
        Assert.True(completed.Passed);
        Assert.Equal(0, completed.GradedExerciseCount);
        Assert.Equal(0, completed.CorrectCount);
        Assert.Equal(1, completed.SelfAssessmentCount);
        Assert.Equal(0, completed.Score);
        Assert.Equal(0, completed.AwardedXp);
        Assert.False(Assert.Single(completed.Results).IsGraded);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.False((await db.AnswerAttempts.SingleAsync(item => item.UserId == state.Account.Id)).IsGraded);
        Assert.Empty(await db.XpTransactions.Where(item => item.UserId == state.Account.Id).ToArrayAsync());
        Assert.Empty(await db.DailyActivities.Where(item => item.UserId == state.Account.Id).ToArrayAsync());
        Assert.Equal("Completed", (await db.LessonProgress.SingleAsync(item => item.UserId == state.Account.Id)).Status);
        var map = (await (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/progress?courseId={state.Content.CourseId}", bearer: state.Token)).Content.ReadFromJsonAsync<CourseProgress>())!;
        Assert.False(map.Lessons.Single(item => item.Id == state.Content.LessonId).HasGradedScore);
    }

    [Fact]
    public async Task MixedLessonCalculatesAccuracyFromGradedAnswersAndAwardsNormalXpOnce()
    {
        using var browser = Browser(); var state = await SetupAsync(browser, ["Translation", "Speaking"]);
        await TextAsync(browser, state.Token, state.Session, "Hola");
        var next = await ReadAsync(browser, state.Token, state.Session.Id);
        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(browser, state.Token, next)).StatusCode);
        var completed = await CompleteAsync(browser, state.Token, next.Id);
        Assert.Equal(1, completed.GradedExerciseCount);
        Assert.Equal(1, completed.GradedAnsweredCount);
        Assert.Equal(1, completed.SelfAssessmentCount);
        Assert.Equal(1, completed.CorrectCount);
        Assert.Equal(2, completed.AnsweredCount);
        Assert.Equal(100, completed.Score);
        Assert.Equal(100, completed.Accuracy);
        Assert.Equal(20, completed.AwardedXp);
        Assert.Equal(20, (await CompleteAsync(browser, state.Token, next.Id)).AwardedXp);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await db.XpTransactions.CountAsync(item => item.UserId == state.Account.Id));
    }

    [Fact]
    public async Task FourSelfAssessmentsCannotTurnOneWrongGradedAnswerIntoAPass()
    {
        using var browser = Browser(); var state = await SetupAsync(browser, ["Translation", "Speaking", "Speaking", "Speaking", "Speaking"]);
        await TextAsync(browser, state.Token, state.Session, "Wrong answer");
        for (int index = 0; index < 4; index++)
        {
            var current = await ReadAsync(browser, state.Token, state.Session.Id);
            Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(browser, state.Token, current)).StatusCode);
        }
        var completed = await CompleteAsync(browser, state.Token, state.Session.Id);
        Assert.False(completed.Passed);
        Assert.Equal(4, completed.Hearts);
        Assert.Equal(0, completed.Score);
        Assert.Equal(0, completed.AwardedXp);
        Assert.Equal(4, completed.SelfAssessmentCount);
    }

    [Fact]
    public async Task PracticeFlagsCannotReplaceGradedAnswersOrBypassSpeakingPayloadAndOrderRules()
    {
        using var browser = Browser(); var state = await SetupAsync(browser, ["Translation", "Speaking"]);
        Assert.Equal(HttpStatusCode.Conflict, (await ConfirmAsync(browser, state.Token, state.Session)).StatusCode);
        await TextAsync(browser, state.Token, state.Session, "Hola");
        var current = await ReadAsync(browser, state.Token, state.Session.Id);
        var invalid = await browser.SendAsync(HttpMethod.Post, Path(current.Id), new
        {
            sessionExerciseId = current.CurrentExercise!.Id,
            selectedOptionIds = Array.Empty<Guid>(),
            answerText = "Claimed score",
            practiceCompleted = true,
            idempotencyKey = Guid.NewGuid()
        }, state.Token);
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        var forged = await browser.SendAsync(HttpMethod.Post, Path(current.Id), new
        {
            sessionExerciseId = current.CurrentExercise.Id,
            selectedOptionIds = Array.Empty<Guid>(),
            practiceCompleted = true,
            isGraded = false,
            idempotencyKey = Guid.NewGuid()
        }, state.Token);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Guid key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(browser, state.Token, current, key)).StatusCode);
        var changedReplay = await browser.SendAsync(HttpMethod.Post, Path(current.Id), new
        {
            sessionExerciseId = current.CurrentExercise.Id,
            selectedOptionIds = Array.Empty<Guid>(),
            practiceCompleted = false,
            idempotencyKey = key
        }, state.Token);
        Assert.Equal(HttpStatusCode.Conflict, changedReplay.StatusCode);
    }

    [Fact]
    public async Task FreeSpeakingPracticeDoesNotSpendHeartsOrEarnRewards()
    {
        using var browser = Browser(); var state = await SetupAsync(browser, ["Speaking"], practice: true);
        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(browser, state.Token, state.Session)).StatusCode);
        var completed = await CompleteAsync(browser, state.Token, state.Session.Id);
        Assert.Equal("Practice", completed.Mode);
        Assert.Equal(0, completed.AwardedXp);
        Assert.Equal(5, completed.Hearts);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Empty(await db.LessonProgress.Where(item => item.UserId == state.Account.Id).ToArrayAsync());
    }

    [Fact]
    public async Task FirstGradedRevisionCanEarnXpAfterUngradedCompletionWithoutDuplicatingRewards()
    {
        using var browser = Browser(); var state = await SetupAsync(browser, ["Speaking"]);
        await ConfirmAsync(browser, state.Token, state.Session);
        await CompleteAsync(browser, state.Token, state.Session.Id);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        // Isolated fixture mutation simulates later graded content; production edits require an independent revision.
        await db.Exercises.Where(item => item.Id == state.Content.ExerciseIds[0]).ExecuteUpdateAsync(update => update.SetProperty(item => item.Type, "Translation").SetProperty(item => item.AudioMediaId, (Guid?)null));
        db.AcceptedAnswers.Add(new AcceptedAnswer { ExerciseId = state.Content.ExerciseIds[0], AnswerText = "Hola" });
        await db.SaveChangesAsync();
        var session = (await (await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{state.Content.LessonId}/sessions", bearer: state.Token)).Content.ReadFromJsonAsync<SessionView>())!;
        await TextAsync(browser, state.Token, session, "Hola");
        Assert.Equal(20, (await CompleteAsync(browser, state.Token, session.Id)).AwardedXp);
        Assert.Equal(1, await db.XpTransactions.CountAsync(item => item.UserId == state.Account.Id));
    }

    [Fact]
    public async Task DatabasePreventsFalseCorrectnessForUngradedPracticeAndLegacyFeedbackDefaultsToGraded()
    {
        using var browser = Browser(); var state = await SetupAsync(browser, ["Speaking"]);
        await ConfirmAsync(browser, state.Token, state.Session);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"AnswerAttempts\" SET \"IsCorrect\" = true WHERE \"UserId\" = {state.Account.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        using var legacyBrowser = Browser(); var legacy = await SetupAsync(legacyBrowser, ["Translation"]);
        await TextAsync(legacyBrowser, legacy.Token, legacy.Session, "Hola");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AnswerAttempts\" SET \"ResponseSnapshot\" = \"ResponseSnapshot\" - 'isGraded' WHERE \"UserId\" = {legacy.Account.Id}");
        Assert.True((await ReadAsync(legacyBrowser, legacy.Token, legacy.Session.Id)).LastFeedback!.Feedback.IsGraded);
        Assert.Equal(100, (await CompleteAsync(legacyBrowser, legacy.Token, legacy.Session.Id)).Score);
    }

    [Fact]
    public async Task SpeakingRequiresAReviewedShortReferenceAndNoAnswerKey()
    {
        using var browser = Browser(); var state = await SetupAsync(browser, ["Speaking"]);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var exercise = await db.Exercises.SingleAsync(item => item.Id == state.Content.ExerciseIds[0]);
        var validation = scope.ServiceProvider.GetRequiredService<LanguageLearning.Infrastructure.Content.LessonValidation>();
        db.AcceptedAnswers.Add(new AcceptedAnswer { ExerciseId = exercise.Id, AnswerText = "Scored answer forbidden" });
        await db.SaveChangesAsync();
        Assert.NotNull(await validation.ValidateAsync(state.Content.VersionId, default));
        await db.AcceptedAnswers.Where(item => item.ExerciseId == exercise.Id).ExecuteDeleteAsync();
        await db.MediaAssets.Where(item => item.Id == exercise.AudioMediaId).ExecuteUpdateAsync(update => update.SetProperty(item => item.DurationMilliseconds, 31000));
        Assert.NotNull(await validation.ValidateAsync(state.Content.VersionId, default));
        await db.Exercises.Where(item => item.Id == exercise.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.AudioMediaId, (Guid?)null));
        Assert.NotNull(await validation.ValidateAsync(state.Content.VersionId, default));
    }

    private SessionTestBrowser Browser() => new(fixture.Accounts.CreateMediaClient(fixture.Storage.Store));
    private sealed record SpeakingState(SecurityTestAccount Account, string Token, LearningTestContent Content, SessionView Session);
    private async Task<SpeakingState> SetupAsync(SessionTestBrowser browser, string[] types, bool practice = false)
    {
        var owner = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        var reviewer = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        var student = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        string token = await student.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture.Accounts, owner.Id, types.Length);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        if (types.Contains("Speaking"))
        {
            Guid mediaId = Guid.NewGuid(); var audio = WaveAudioInspector.Inspect(WaveTestAudio.Create());
            await fixture.Storage.Store.UploadReservedAsync(mediaId, audio, default);
            // Reviewed reference metadata is an isolated fixture; MediaWorkflowTests cover actual independent review APIs.
            db.MediaAssets.Add(new MediaAsset
            {
                Id = mediaId,
                StorageKey = $"media/{mediaId:N}",
                MimeType = audio.MimeType,
                ByteSize = audio.Bytes.Length,
                Source = "Generated fixture reference",
                License = "Original test audio",
                LanguageId = 2,
                UploadedBy = owner.Id,
                IsVerified = true,
                VerifiedBy = reviewer.Id,
                VerifiedAt = DateTimeOffset.UtcNow,
                DurationMilliseconds = audio.DurationMilliseconds,
                Sha256 = Convert.ToHexString(SHA256.HashData(audio.Bytes.Span)).ToLowerInvariant()
            });
            await db.SaveChangesAsync();
            for (int index = 0; index < types.Length; index++)
                if (types[index] == "Speaking")
                {
                    Guid exerciseId = content.ExerciseIds[index];
                    await db.Exercises.Where(item => item.Id == exerciseId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Type, "Speaking").SetProperty(item => item.AudioMediaId, mediaId));
                    await db.AcceptedAnswers.Where(item => item.ExerciseId == exerciseId).ExecuteDeleteAsync();
                }
        }
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = content.CourseId }, token)).EnsureSuccessStatusCode();
        var response = practice ? await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", new { lessonId = content.LessonId }, token)
            : await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: token);
        response.EnsureSuccessStatusCode();
        return new(student, token, content, (await response.Content.ReadFromJsonAsync<SessionView>())!);
    }
    private static string Path(Guid session) => $"/api/v1/me/learning-sessions/{session}/answers";
    private static Task<HttpResponseMessage> ConfirmAsync(SessionTestBrowser browser, string token, SessionView session, Guid? key = null) => browser.SendAsync(HttpMethod.Post, Path(session.Id),
        new { sessionExerciseId = session.CurrentExercise!.Id, selectedOptionIds = Array.Empty<Guid>(), practiceCompleted = true, idempotencyKey = key ?? Guid.NewGuid() }, token);
    private static async Task TextAsync(SessionTestBrowser browser, string token, SessionView session, string text) =>
        (await browser.SendAsync(HttpMethod.Post, Path(session.Id), new { sessionExerciseId = session.CurrentExercise!.Id, answerText = text, selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
    private static async Task<SessionView> ReadAsync(SessionTestBrowser browser, string token, Guid session) =>
        (await (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session}", bearer: token)).Content.ReadFromJsonAsync<SessionView>())!;
    private static async Task<SessionView> CompleteAsync(SessionTestBrowser browser, string token, Guid session)
    {
        var response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session}/complete", bearer: token);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<SessionView>())!;
    }
}
