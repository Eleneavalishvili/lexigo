using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Speech;
using LanguageLearning.Infrastructure.Storage;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class SpeechCheckTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private sealed class Recognizer : ISpeechRecognizer
    {
        public SpeechRecognitionResult Result { get; set; } = new(SpeechRecognitionStatus.Recognized, "Hola");
        public int Calls;
        public Task<SpeechRecognitionResult> RecognizeAsync(string language, PreparedAudio audio, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Assert.Equal("es", language);
            Assert.Equal(16000, audio.SampleRate);
            return Task.FromResult(Result);
        }
    }

    private async Task<(SecurityTestAccount User, string Token, SessionView Session)> Setup(SessionTestBrowser browser, bool practice = false, int count = 1)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var reviewer = await SecurityTestAccount.CreateAsync(fixture);
        string token = await user.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id, count, type: "SpeakingCheck");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        // Synthetic approved-reference facts in an isolated database; actual media playback is tested separately.
        var media = new MediaAsset
        {
            UploadedBy = user.Id,
            VerifiedBy = reviewer.Id,
            VerifiedAt = DateTimeOffset.UtcNow,
            IsVerified = true,
            LanguageId = 2,
            StorageKey = "fixture/" + Guid.NewGuid(),
            MimeType = "audio/wav",
            ByteSize = 3244,
            DurationMilliseconds = 100,
            Sha256 = new string('a', 64),
            Source = "Synthetic speech-check fixture",
            License = "Original test fixture"
        };
        db.MediaAssets.Add(media); await db.SaveChangesAsync();
        await db.Exercises.Where(item => content.ExerciseIds.Contains(item.Id)).ExecuteUpdateAsync(update => update.SetProperty(item => item.AudioMediaId, media.Id));
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token)).EnsureSuccessStatusCode();
        var response = practice ? await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", new { content.LessonId }, token)
            : await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: token);
        response.EnsureSuccessStatusCode();
        return (user, token, (await response.Content.ReadFromJsonAsync<SessionView>())!);
    }

    private static byte[] Recording() => WaveTestAudio.Create(frames: 1600, sampleRate: 16000);
    private static Task<HttpResponseMessage> Check(SessionTestBrowser browser, string token, SessionView session, Guid key, byte[]? bytes = null, Guid? exercise = null, string? extra = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent((exercise ?? session.CurrentExercise!.Id).ToString()), "ExerciseId");
        form.Add(new StringContent(key.ToString()), "IdempotencyKey");
        form.Add(new ByteArrayContent(bytes ?? Recording()), "Recording", "voice.wav");
        if (extra is not null) form.Add(new StringContent("true"), extra);
        return browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/speech-checks", form, token);
    }

    [Fact]
    public async Task OneMissIsFreeFurtherMissCostsAHeartAndOnlyMatchedAudioAdvances()
    {
        var engine = new Recognizer { Result = new(SpeechRecognitionStatus.Recognized, "Adiós") };
        using var browser = new SessionTestBrowser(fixture.CreateSpeechClient(engine));
        var state = await Setup(browser);
        var first = (await (await Check(browser, state.Token, state.Session, Guid.NewGuid())).Content.ReadFromJsonAsync<SpeechCheckFeedback>())!;
        Assert.Equal("Mismatch", first.Status); Assert.Equal(5, first.HeartsRemaining); Assert.Null(first.Answer);
        var second = (await (await Check(browser, state.Token, state.Session, Guid.NewGuid())).Content.ReadFromJsonAsync<SpeechCheckFeedback>())!;
        Assert.Equal(4, second.HeartsRemaining);
        var pending = (await (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{state.Session.Id}", bearer: state.Token)).Content.ReadFromJsonAsync<SessionView>())!;
        Assert.Equal(0, pending.AnsweredCount); Assert.Equal(2, pending.SpeakingMissCount);
        engine.Result = new(SpeechRecognitionStatus.Recognized, "Hola");
        Guid key = Guid.NewGuid();
        var accepted = (await (await Check(browser, state.Token, state.Session, key)).Content.ReadFromJsonAsync<SpeechCheckFeedback>())!;
        Assert.Equal("Matched", accepted.Status); Assert.True(accepted.Answer!.IsCorrect); Assert.Equal(4, accepted.HeartsRemaining);
        var repeated = await Check(browser, state.Token, state.Session, key); repeated.EnsureSuccessStatusCode();
        Assert.Equal(3, engine.Calls);
        var complete = (await (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{state.Session.Id}/complete", bearer: state.Token)).Content.ReadFromJsonAsync<SessionView>())!;
        Assert.Equal(100, complete.Score); Assert.Equal(1, complete.GradedAnsweredCount); Assert.Equal(20, complete.AwardedXp);
    }

    [Fact]
    public async Task ConcurrentReplayChargesOnceAndChangedBytesConflict()
    {
        var engine = new Recognizer { Result = new(SpeechRecognitionStatus.Recognized, "Adiós") };
        using var browser = new SessionTestBrowser(fixture.CreateSpeechClient(engine)); var state = await Setup(browser);
        (await Check(browser, state.Token, state.Session, Guid.NewGuid())).EnsureSuccessStatusCode();
        Guid key = Guid.NewGuid();
        var responses = await Task.WhenAll(Check(browser, state.Token, state.Session, key), Check(browser, state.Token, state.Session, key));
        foreach (var response in responses) Assert.Equal(4, (await response.Content.ReadFromJsonAsync<SpeechCheckFeedback>())!.HeartsRemaining);
        Assert.Equal(2, engine.Calls);
        byte[] changed = Recording(); changed[^1] ^= 1;
        Assert.Equal(HttpStatusCode.Conflict, (await Check(browser, state.Token, state.Session, key, changed)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await db.SpeechCheckAttempts.Where(item => item.UserId == state.User.Id).SumAsync(item => item.HeartsCharged));
    }

    [Fact]
    public async Task SilenceAndUnavailableChecksDoNotSpendTheFreeRetryOrHearts()
    {
        var engine = new Recognizer { Result = new(SpeechRecognitionStatus.NoSpeech) };
        using var browser = new SessionTestBrowser(fixture.CreateSpeechClient(engine)); var state = await Setup(browser);
        Guid key = Guid.NewGuid();
        var silent = (await (await Check(browser, state.Token, state.Session, key)).Content.ReadFromJsonAsync<SpeechCheckFeedback>())!;
        Assert.Equal("NoSpeech", silent.Status); Assert.True(silent.FreeRetryAvailable); Assert.Equal(5, silent.HeartsRemaining);
        (await Check(browser, state.Token, state.Session, key)).EnsureSuccessStatusCode(); Assert.Equal(1, engine.Calls);
        engine.Result = new(SpeechRecognitionStatus.Busy);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Check(browser, state.Token, state.Session, Guid.NewGuid())).StatusCode);
        engine.Result = new(SpeechRecognitionStatus.Recognized, "Adiós");
        Assert.Equal(5, (await (await Check(browser, state.Token, state.Session, Guid.NewGuid())).Content.ReadFromJsonAsync<SpeechCheckFeedback>())!.HeartsRemaining);
    }

    [Fact]
    public async Task ForeignFutureTypedAndForgedSubmissionsCannotTriggerRecognition()
    {
        var engine = new Recognizer();
        using var browser = new SessionTestBrowser(fixture.CreateSpeechClient(engine)); var state = await Setup(browser, count: 2);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid future = await db.SessionExercises.Where(item => item.SessionId == state.Session.Id).OrderByDescending(item => item.OrderNumber).Select(item => item.Id).FirstAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await Check(browser, state.Token, state.Session, Guid.NewGuid(), exercise: future)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Check(browser, state.Token, state.Session, Guid.NewGuid(), extra: "IsCorrect")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Check(browser, state.Token, state.Session, Guid.NewGuid(), bytes: [1, 2, 3])).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{state.Session.Id}/answers",
            new { sessionExerciseId = state.Session.CurrentExercise!.Id, answerText = "Hola", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, state.Token)).StatusCode);
        using var outsider = new SessionTestBrowser(fixture.CreateSpeechClient(engine));
        var stranger = await SecurityTestAccount.CreateAsync(fixture); string otherToken = await stranger.LoginAsync(outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await Check(outsider, otherToken, state.Session, Guid.NewGuid())).StatusCode);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public async Task SixMissesExhaustHeartsWithoutAdvancingAndFreePracticeDoesNotCharge()
    {
        var engine = new Recognizer { Result = new(SpeechRecognitionStatus.Recognized, "Adiós") };
        using var browser = new SessionTestBrowser(fixture.CreateSpeechClient(engine)); var state = await Setup(browser);
        for (int index = 0; index < 6; index++) (await Check(browser, state.Token, state.Session, Guid.NewGuid())).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Check(browser, state.Token, state.Session, Guid.NewGuid())).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var balance = await db.LearnerHearts.SingleAsync(item => item.UserId == state.User.Id);
        Assert.Equal(0, balance.Count); Assert.NotNull(balance.RefillAt);
        Assert.False(await db.AnswerAttempts.AnyAsync(item => item.UserId == state.User.Id));
        using var practiceBrowser = new SessionTestBrowser(fixture.CreateSpeechClient(engine)); var practice = await Setup(practiceBrowser, practice: true);
        for (int index = 0; index < 3; index++) Assert.Equal(5, (await (await Check(practiceBrowser, practice.Token, practice.Session, Guid.NewGuid())).Content.ReadFromJsonAsync<SpeechCheckFeedback>())!.HeartsRemaining);
        Assert.Equal(0, await db.SpeechCheckAttempts.Where(item => item.UserId == practice.User.Id).SumAsync(item => item.HeartsCharged));
    }

    [Fact]
    public async Task PublicationRequiresReviewedShortReferenceAndPrivateAcceptedWords()
    {
        var engine = new Recognizer(); using var browser = new SessionTestBrowser(fixture.CreateSpeechClient(engine)); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var validation = new LanguageLearning.Infrastructure.Content.LessonValidation(db);
        Assert.Null(await validation.ValidateAsync(state.Session.VersionId, default));
        var exercise = await db.Exercises.SingleAsync(item => item.LessonVersionId == state.Session.VersionId);
        Guid mediaId = exercise.AudioMediaId!.Value;
        await db.MediaAssets.Where(item => item.Id == mediaId).ExecuteUpdateAsync(update => update.SetProperty(item => item.DurationMilliseconds, 31000));
        Assert.NotNull(await validation.ValidateAsync(state.Session.VersionId, default));
        await db.MediaAssets.Where(item => item.Id == mediaId).ExecuteUpdateAsync(update => update.SetProperty(item => item.DurationMilliseconds, 100));
        await db.AcceptedAnswers.Where(item => item.ExerciseId == exercise.Id).ExecuteDeleteAsync();
        Assert.NotNull(await validation.ValidateAsync(state.Session.VersionId, default));
    }

    [Fact]
    public async Task PerUserQuotaBoundsRepeatedSilenceWithoutAdvancingOrCharging()
    {
        var engine = new Recognizer { Result = new(SpeechRecognitionStatus.NoSpeech) };
        using var browser = new SessionTestBrowser(fixture.CreateSpeechClient(engine)); var state = await Setup(browser);
        Guid firstKey = Guid.NewGuid();
        (await Check(browser, state.Token, state.Session, firstKey)).EnsureSuccessStatusCode();
        for (int index = 1; index < 30; index++) (await Check(browser, state.Token, state.Session, Guid.NewGuid())).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Check(browser, state.Token, state.Session, Guid.NewGuid())).StatusCode);
        (await Check(browser, state.Token, state.Session, firstKey)).EnsureSuccessStatusCode();
        Assert.Equal(30, engine.Calls);
    }

    [Fact]
    public async Task DatabaseRefusesCrossOwnerEvidenceAndTechnicalFailureCharges()
    {
        var engine = new Recognizer(); using var browser = new SessionTestBrowser(fixture.CreateSpeechClient(engine)); var state = await Setup(browser);
        var stranger = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var ownership = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"SpeechCheckAttempts\" (\"Id\",\"UserId\",\"SessionExerciseId\",\"IdempotencyKey\",\"AudioSha256\",\"Status\",\"HeartsCharged\",\"ResponseSnapshot\",\"CreatedAt\") VALUES ({Guid.NewGuid()},{stranger.Id},{state.Session.CurrentExercise!.Id},{Guid.NewGuid()},{new string('a', 64)},'Matched',0,'{{}}'::jsonb,{DateTimeOffset.UtcNow})"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ownership.SqlState);
        var charge = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"SpeechCheckAttempts\" (\"Id\",\"UserId\",\"SessionExerciseId\",\"IdempotencyKey\",\"AudioSha256\",\"Status\",\"HeartsCharged\",\"ResponseSnapshot\",\"CreatedAt\") VALUES ({Guid.NewGuid()},{state.User.Id},{state.Session.CurrentExercise!.Id},{Guid.NewGuid()},{new string('a', 64)},'NoSpeech',1,'{{}}'::jsonb,{DateTimeOffset.UtcNow})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, charge.SqlState);
    }
}
