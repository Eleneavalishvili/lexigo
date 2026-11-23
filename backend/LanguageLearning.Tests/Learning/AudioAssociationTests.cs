using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using LanguageLearning.Api.Vocabulary;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Storage;
using LanguageLearning.Infrastructure.Vocabulary;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class AudioAssociationTests(MediaWorkflowFixture fixture) : IClassFixture<MediaWorkflowFixture>
{
    private int counter;
    private readonly Dictionary<Guid, byte[]> clips = [];

    [Fact]
    public async Task ReviewedWordRecordingOpensARestrictedSignedLink()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser();
        var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        Guid audioId = await AudioAsync(writer.Account.Id, reader.Account.Id);
        Guid foreignAudioId = await AudioAsync(writer.Account.Id, reader.Account.Id, language: 3);
        using var admin = Browser();
        var manager = await StaffAsync(admin, RoleNames.Admin);
        var content = await LearningTestContent.CreateAsync(fixture.Accounts, writer.Account.Id, count: 1);
        using var scope = fixture.Accounts.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var word = new VocabularyItem
        {
            CourseId = content.CourseId,
            Word = "agua",
            EnglishTranslation = "water"
        };
        db.VocabularyItems.Add(word);
        await db.SaveChangesAsync();

        string manage = $"/api/v1/manage/vocabulary/{word.Id}/audio";
        var valid = new VocabularyAudioRequest(audioId, null);
        Assert.Equal(HttpStatusCode.Forbidden, (await author.SendAsync(HttpMethod.Put, manage, valid, writer.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Put, manage,
            new VocabularyAudioRequest(foreignAudioId, null), manager.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(HttpMethod.Put, manage, valid, manager.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(HttpMethod.Put, manage, valid, manager.Token)).StatusCode);
        var staffList = await admin.SendAsync(HttpMethod.Get, $"/api/v1/manage/vocabulary?courseId={content.CourseId}", bearer: manager.Token);
        staffList.EnsureSuccessStatusCode();
        Assert.Equal(audioId, (await staffList.Content.ReadFromJsonAsync<StaffWordPage>())!.Items.Single(item => item.Id == word.Id).AudioMediaId);

        using var learner = Browser();
        var student = await StaffAsync(learner, RoleNames.Learner);
        var response = await learner.SendAsync(HttpMethod.Get,
            $"/api/v1/vocabulary/{word.Id}/audio", bearer: student.Token);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var link = (await response.Content.ReadFromJsonAsync<WordAudioLink>())!;
        Assert.Equal(300, link.ExpiresInSeconds);
        Assert.Equal("Original test recording", link.License);
        using var http = new HttpClient();
        Assert.Equal(clips[audioId], await http.GetByteArrayAsync(link.Url));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Put, manage,
            new VocabularyAudioRequest(null, Guid.NewGuid()), manager.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(HttpMethod.Put, manage,
            new VocabularyAudioRequest(null, audioId), manager.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await learner.SendAsync(HttpMethod.Get,
            $"/api/v1/vocabulary/{word.Id}/audio", bearer: student.Token)).StatusCode);
        Assert.Equal(2, await db.AuditLogs.CountAsync(item => item.EntityType == "VocabularyItem" &&
            item.EntityId == word.Id.ToString()));
    }

    [Fact]
    public async Task AudioMatchingPublishesPlaysGradesAndCopiesReviewedReferencesIntoRevisions()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        using var admin = Browser(); var manager = await StaffAsync(admin, RoleNames.Admin);
        Guid first = await AudioAsync(writer.Account.Id, reader.Account.Id);
        Guid second = await AudioAsync(writer.Account.Id, reader.Account.Id);
        var content = await DraftAsync(writer.Account.Id);
        Assert.Equal(HttpStatusCode.OK, (await SaveMatchingAsync(author, writer.Token, content, first, second)).StatusCode);
        await PublishAsync(author, writer.Token, reviewer, reader.Token, admin, manager.Token, content);
        Assert.Equal(HttpStatusCode.Created, (await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{content.LessonId}/versions", bearer: writer.Token)).StatusCode);
        var revision = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{content.LessonId}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        var copied = Assert.Single(revision.Exercises).Options.Where(item => item.AudioMediaId.HasValue).ToArray();
        Assert.Equal(2, copied.Length);
        Assert.Contains(copied, item => item.AudioMediaId == first);
        Assert.Contains(copied, item => item.AudioMediaId == second);
        using var learner = Browser(); var student = await StaffAsync(learner, RoleNames.Learner);
        var session = await StartAsync(learner, student.Token, content);
        var view = session.CurrentExercise!;
        var audioOptions = view.Options.Where(item => item.Audio is not null).ToArray();
        Assert.Equal(2, audioOptions.Length);
        Assert.All(audioOptions, item => Assert.Equal("left", item.Side));
        var json = JsonSerializer.Serialize(view, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("matchKey", json);
        Assert.DoesNotContain("storageKey", json);
        var response = await learner.SendAsync(HttpMethod.Get, OptionPath(session.Id, view.Id, audioOptions[0].Id), bearer: student.Token);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var link = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using var http = new HttpClient();
        Assert.True((await http.GetByteArrayAsync(link.RootElement.GetProperty("url").GetString())).Length > 44);
        Assert.Equal(HttpStatusCode.NotFound, (await learner.SendAsync(HttpMethod.Get, OptionPath(session.Id, view.Id, first), bearer: student.Token)).StatusCode);
        using var outsider = Browser(); var stranger = await StaffAsync(outsider, RoleNames.Learner);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, OptionPath(session.Id, view.Id, audioOptions[0].Id), bearer: stranger.Token)).StatusCode);
        var pairs = view.Options.Where(item => item.Side == "left").Select(item => new MatchedPair(item.Id,
            view.Options.Single(target => target.Side == "right" && target.Text == (item.Text == "Clip one" ? "Hello" : "Goodbye")).Id)).ToArray();
        Guid key = Guid.NewGuid();
        var answer = await learner.SendAsync(HttpMethod.Post, AnswersPath(session.Id), new { sessionExerciseId = view.Id, selectedOptionIds = Array.Empty<Guid>(), matchedPairs = pairs, idempotencyKey = key }, student.Token);
        Assert.True((await answer.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect);
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Post, AnswersPath(session.Id), new { sessionExerciseId = view.Id, selectedOptionIds = Array.Empty<Guid>(), matchedPairs = pairs.Reverse().ToArray(), idempotencyKey = key }, student.Token)).StatusCode);
        var result = (await (await learner.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: student.Token)).Content.ReadFromJsonAsync<SessionView>())!;
        Assert.Equal(100, result.Score);
        Assert.Equal(20, result.AwardedXp);
        Assert.Equal(2, Assert.Single(result.Results).Options!.Count(item => item.Audio is not null));
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Get, OptionPath(session.Id, view.Id, audioOptions[0].Id), bearer: student.Token)).StatusCode);
    }

    [Fact]
    public async Task SavedOptionReferencesSurviveSourceChangesAndPreventObjectDeletion()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        using var admin = Browser(); var manager = await StaffAsync(admin, RoleNames.Admin);
        Guid first = await AudioAsync(writer.Account.Id, reader.Account.Id);
        Guid second = await AudioAsync(writer.Account.Id, reader.Account.Id);
        var content = await DraftAsync(writer.Account.Id);
        await SaveMatchingAsync(author, writer.Token, content, first, second);
        await PublishAsync(author, writer.Token, reviewer, reader.Token, admin, manager.Token, content);
        using var learner = Browser(); var student = await StaffAsync(learner, RoleNames.Learner);
        var session = await StartAsync(learner, student.Token, content);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var saved = await db.SessionOptionAudio.SingleAsync(item => item.SessionExerciseId == session.CurrentExercise!.Id && item.AudioMediaId == first);
        // Isolated mutation proves snapshots do not depend on a mutable source-option reference.
        await db.ExerciseOptions.Where(item => item.AudioMediaId == first).ExecuteUpdateAsync(update => update.SetProperty(item => item.AudioMediaId, (Guid?)null));
        Assert.Equal(HttpStatusCode.Conflict, (await author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{first}", bearer: writer.Token)).StatusCode);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"MediaAssets\" WHERE \"Id\" = {first}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        var response = await learner.SendAsync(HttpMethod.Get, OptionPath(session.Id, saved.SessionExerciseId, saved.OptionId), bearer: student.Token);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using var http = new HttpClient();
        Assert.Equal(clips[first], await http.GetByteArrayAsync(body.RootElement.GetProperty("url").GetString()));
    }

    [Fact]
    public async Task ForeignWrongLanguageUnreviewedAndDuplicateAudioCannotBeAttached()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var other = Browser(); var stranger = await StaffAsync(other, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var content = await DraftAsync(writer.Account.Id);
        Guid first = await AudioAsync(writer.Account.Id, reader.Account.Id);
        foreach (string kind in new[] { "foreign", "language", "unreviewed", "same-id", "same-bytes" })
        {
            Guid second = kind == "same-id" ? first : await AudioAsync(kind == "foreign" ? stranger.Account.Id : writer.Account.Id,
                reader.Account.Id, frames: kind == "same-bytes" ? 9000 : null);
            using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            Guid left = first;
            if (kind == "same-bytes") left = await AudioAsync(writer.Account.Id, reader.Account.Id, frames: 9000);
            if (kind == "language") await db.MediaAssets.Where(item => item.Id == second).ExecuteUpdateAsync(update => update.SetProperty(item => item.LanguageId, 3));
            if (kind == "unreviewed") await db.MediaAssets.Where(item => item.Id == second).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsVerified, false).SetProperty(item => item.VerifiedBy, (Guid?)null).SetProperty(item => item.VerifiedAt, (DateTimeOffset?)null));
            Assert.Equal(HttpStatusCode.Conflict, (await SaveMatchingAsync(author, writer.Token, content, left, second)).StatusCode);
        }
    }

    [Fact]
    public async Task MatchingAttachmentAndAdministratorDeletionCannotBothSucceed()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        using var admin = Browser(); var manager = await StaffAsync(admin, RoleNames.Admin);
        var content = await DraftAsync(writer.Account.Id);
        Guid first = await AudioAsync(writer.Account.Id, reader.Account.Id);
        Guid second = await AudioAsync(writer.Account.Id, reader.Account.Id);
        var results = await Task.WhenAll(SaveMatchingAsync(author, writer.Token, content, first, second), admin.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{first}", bearer: manager.Token));
        Assert.Single(results, item => item.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, item => item.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RussianSoundAssociationKeepsYoDistinctAndUsesNormalServerChoiceGrading()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor, 5);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer, 5);
        using var admin = Browser(); var manager = await StaffAsync(admin, RoleNames.Admin, 5);
        Guid media = await AudioAsync(writer.Account.Id, reader.Account.Id, language: 5);
        var content = await DraftAsync(writer.Account.Id, "Alphabet", course: 4);
        Assert.Equal(HttpStatusCode.OK, (await SaveAlphabetAsync(author, writer.Token, content, media, ["Ё", "Е"], "Ё")).StatusCode);
        await PublishAsync(author, writer.Token, reviewer, reader.Token, admin, manager.Token, content);
        using var learner = Browser(); var student = await StaffAsync(learner, RoleNames.Learner, 5);
        var session = await StartAsync(learner, student.Token, content);
        Assert.NotNull(session.CurrentExercise!.Audio);
        var correct = session.CurrentExercise.Options.Single(item => item.Text == "Ё");
        var answer = await learner.SendAsync(HttpMethod.Post, AnswersPath(session.Id), new { sessionExerciseId = session.CurrentExercise.Id, selectedOptionIds = new[] { correct.Id }, idempotencyKey = Guid.NewGuid() }, student.Token);
        Assert.True((await answer.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect);
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}/exercises/{session.CurrentExercise.Id}/audio", bearer: student.Token)).StatusCode);
    }

    [Fact]
    public async Task SoundQuestionsRejectCaseOnlyDecoysAndNonRussianCoursesWhileVisualModeRemainsValid()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor, 5);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer, 5);
        var content = await DraftAsync(writer.Account.Id, "Alphabet", course: 4);
        Guid media = await AudioAsync(writer.Account.Id, reader.Account.Id, language: 5);
        await SaveAlphabetAsync(author, writer.Token, content, media, ["А", "а"], "А");
        Assert.Equal(HttpStatusCode.Conflict, (await SubmitAsync(author, writer.Token, content)).StatusCode);
        await SaveAlphabetAsync(author, writer.Token, content, null, ["А", "а"], "А");
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(author, writer.Token, content)).StatusCode);
        using var spanishAuthor = Browser(); var spanish = await StaffAsync(spanishAuthor, RoleNames.ContentAuthor);
        var nonRussian = await DraftAsync(spanish.Account.Id, "Alphabet");
        Guid spanishMedia = await AudioAsync(spanish.Account.Id, reader.Account.Id);
        await SaveAlphabetAsync(spanishAuthor, spanish.Token, nonRussian, spanishMedia, ["Ё", "Е"], "Ё");
        Assert.Equal(HttpStatusCode.Conflict, (await SubmitAsync(spanishAuthor, spanish.Token, nonRussian)).StatusCode);
    }

    private SessionTestBrowser Browser() => new(fixture.Accounts.CreateMediaClient(fixture.Storage.Store));
    private async Task<(SecurityTestAccount Account, string Token)> StaffAsync(SessionTestBrowser browser, string role, int language = 2)
    {
        var account = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        using var scope = fixture.Accounts.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (role != RoleNames.Learner) await users.AddToRoleAsync((await users.FindByIdAsync(account.Id.ToString()))!, role);
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        db.UserLanguageScopes.Add(new UserLanguageScope { UserId = account.Id, LanguageId = language }); await db.SaveChangesAsync();
        return (account, await account.LoginAsync(browser));
    }
    private async Task<Guid> AudioAsync(Guid owner, Guid reviewer, int language = 2, int? frames = null)
    {
        Guid id = Guid.NewGuid(); var audio = WaveAudioInspector.Inspect(WaveTestAudio.Create(frames: frames ?? 800 + ++counter));
        clips[id] = audio.Bytes.ToArray(); await fixture.Storage.Store.UploadReservedAsync(id, audio, default);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = id,
            UploadedBy = owner,
            VerifiedBy = reviewer,
            VerifiedAt = DateTimeOffset.UtcNow,
            IsVerified = true,
            StorageKey = $"media/{id:N}",
            MimeType = audio.MimeType,
            ByteSize = audio.Bytes.Length,
            LanguageId = language,
            Source = "Synthetic association reference",
            License = "Original test recording",
            DurationMilliseconds = audio.DurationMilliseconds,
            Sha256 = Convert.ToHexString(SHA256.HashData(audio.Bytes.Span)).ToLowerInvariant()
        });
        await db.SaveChangesAsync(); return id;
    }
    private async Task<LearningTestContent> DraftAsync(Guid owner, string type = "Matching", int course = 1)
    {
        var content = await LearningTestContent.CreateAsync(fixture.Accounts, owner, count: 1, type: type, courseNumber: course);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.LessonVersions.Where(item => item.Id == content.VersionId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Draft"));
        await db.Lessons.Where(item => item.Id == content.LessonId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Draft").SetProperty(item => item.CurrentVersionId, (Guid?)null));
        return content;
    }
    private static Task<HttpResponseMessage> SaveMatchingAsync(SessionTestBrowser browser, string token, LearningTestContent content, Guid first, Guid second) => browser.SendAsync(HttpMethod.Put,
        $"/api/v1/manage/lessons/{content.LessonId}/exercises/{content.ExerciseIds[0]}", new
        {
            versionId = content.VersionId,
            type = "Matching",
            prompt = "Listen and connect",
            explanation = "Connect each recording to its meaning.",
            orderNumber = 1,
            options = Array.Empty<string>(),
            answers = Array.Empty<string>(),
            matchPairs = new[] { new { left = "Clip one", right = "Hello", leftAudioMediaId = first }, new { left = "Clip two", right = "Goodbye", leftAudioMediaId = second } }
        }, token);
    private static Task<HttpResponseMessage> SaveAlphabetAsync(SessionTestBrowser browser, string token, LearningTestContent content, Guid? media, string[] options, string answer) => browser.SendAsync(HttpMethod.Put,
        $"/api/v1/manage/lessons/{content.LessonId}/exercises/{content.ExerciseIds[0]}", new { versionId = content.VersionId, type = "Alphabet", prompt = "Identify the letter", explanation = "Keep Russian letters distinct.", orderNumber = 1, options, answers = new[] { answer }, audioMediaId = media }, token);
    private static Task<HttpResponseMessage> SubmitAsync(SessionTestBrowser browser, string token, LearningTestContent content) => browser.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{content.LessonId}/submit", new { versionId = content.VersionId }, token);
    private static async Task PublishAsync(SessionTestBrowser author, string token, SessionTestBrowser reviewer, string reviewToken, SessionTestBrowser admin, string adminToken, LearningTestContent content)
    {
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(author, token, content)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{content.LessonId}/reviews", new { versionId = content.VersionId, decision = "Approved", comment = "Reviewed synthetic association fixture." }, reviewToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{content.LessonId}/publish", new { versionId = content.VersionId }, adminToken)).StatusCode);
    }
    private static async Task<SessionView> StartAsync(SessionTestBrowser browser, string token, LearningTestContent content)
    {
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = content.CourseId }, token)).EnsureSuccessStatusCode();
        var response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: token);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<SessionView>())!;
    }
    private static string OptionPath(Guid session, Guid exercise, Guid option) => $"/api/v1/me/learning-sessions/{session}/exercises/{exercise}/options/{option}/audio";
    private static string AnswersPath(Guid session) => $"/api/v1/me/learning-sessions/{session}/answers";
}
