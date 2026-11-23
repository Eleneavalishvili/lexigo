using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Speech;
using LanguageLearning.Infrastructure.Storage;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task PreviewSpeechChecksScopedDraftWithoutSavingLearnerActivity()
    {
        var recognizer = new PreviewRecognizer();
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = new SessionTestBrowser(fixture.CreateSpeechClient(recognizer));
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var other = new SessionTestBrowser(fixture.CreateSpeechClient(recognizer));
        var anotherWriter = await StaffAsync(other, RoleNames.ContentAuthor);
        var draft = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token),
            "SpeakingCheck", "Say Hola.", [], ["Hola"]);
        var exercise = Assert.Single(draft.Exercises);
        string route = $"/api/v1/manage/lessons/{draft.Id}/versions/{draft.VersionId}/exercises/{exercise.Id}/preview-speech";

        async Task<HttpResponseMessage> Check(SessionTestBrowser browser, string token, byte[]? bytes = null)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(bytes ?? WaveTestAudio.Create(frames: 1600, sampleRate: 16000)), "recording", "voice.wav");
            return await browser.SendAsync(HttpMethod.Post, route, form, token);
        }
        using var denied = await Check(other, anotherWriter.Token);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var invalid = await Check(author, writer.Token, new byte[44]);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, recognizer.Calls);

        foreach (var (recognized, expected) in new[]
        {
            (new SpeechRecognitionResult(SpeechRecognitionStatus.Recognized, "Hola"), "Matched"),
            (new SpeechRecognitionResult(SpeechRecognitionStatus.Recognized, "Adiós"), "Mismatch"),
            (new SpeechRecognitionResult(SpeechRecognitionStatus.NoSpeech), "NoSpeech")
        })
        {
            recognizer.Result = recognized;
            using var response = await Check(author, writer.Token);
            response.EnsureSuccessStatusCode();
            Assert.Equal(expected, (await response.Content.ReadFromJsonAsync<PreviewSpeechFeedback>())!.Status);
        }
        recognizer.Result = new(SpeechRecognitionStatus.Unavailable);
        using var unavailable = await Check(author, writer.Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal(4, recognizer.Calls);

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.False(await database.SpeechCheckAttempts.AnyAsync(item => item.UserId == writer.Account.Id));
        Assert.False(await database.AnswerAttempts.AnyAsync(item => item.UserId == writer.Account.Id));
        Assert.False(await database.LearningSessions.AnyAsync(item => item.UserId == writer.Account.Id));
        Assert.False(await database.XpTransactions.AnyAsync(item => item.UserId == writer.Account.Id));
    }

    private sealed class PreviewRecognizer : ISpeechRecognizer
    {
        public SpeechRecognitionResult Result { get; set; } = new(SpeechRecognitionStatus.NoSpeech);
        public int Calls { get; private set; }

        public Task<SpeechRecognitionResult> RecognizeAsync(string language, PreparedAudio audio,
            CancellationToken cancellationToken)
        {
            Assert.Equal("es", language);
            Assert.Equal(16000, audio.SampleRate);
            Calls++;
            return Task.FromResult(Result);
        }
    }
}
