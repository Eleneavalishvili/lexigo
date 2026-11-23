using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Storage;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class ListeningTests(MediaWorkflowFixture fixture) : IClassFixture<MediaWorkflowFixture>
{
    [Fact]
    public async Task ReviewedChoiceAudioRunsThroughDraftPublicationPlaybackAndScoringForFreeLearner()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        using var admin = Browser(); var manager = await StaffAsync(admin, RoleNames.Admin);
        Guid mediaId = await AudioAsync(writer.Account.Id, reader.Account.Id);
        var content = await DraftAsync(writer.Account.Id);
        var update = await SaveAsync(author, writer.Token, content, mediaId, ["Hola", "Adiós"]);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{content.LessonId}/submit", new { versionId = content.VersionId }, writer.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{content.LessonId}/reviews", new { versionId = content.VersionId, decision = "Approved", comment = "Reviewed audio, choices and license." }, reader.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{content.LessonId}/publish", new { versionId = content.VersionId }, manager.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{content.LessonId}/versions", bearer: writer.Token)).StatusCode);
        var revision = await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{content.LessonId}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>();
        Assert.Equal(mediaId, Assert.Single(revision!.Exercises).AudioMediaId);
        Assert.Equal("Listening", revision.Exercises[0].Type);
        using var learner = Browser(); var student = await StaffAsync(learner, RoleNames.Learner);
        var session = await StartAsync(learner, student.Token, content);
        var exercise = session.CurrentExercise!;
        Assert.Equal("Listening", exercise.Type);
        Assert.Equal("Original test recording", exercise.Audio?.Source);
        string json = JsonSerializer.Serialize(session, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("storageKey", json);
        Assert.DoesNotContain("correctOptionIds", json);
        Assert.DoesNotContain("answers", json);
        var clip = await learner.SendAsync(HttpMethod.Get, AudioPath(session.Id, exercise.Id), bearer: student.Token);
        Assert.True(clip.Headers.CacheControl?.NoStore);
        using var link = JsonDocument.Parse(await clip.Content.ReadAsStringAsync());
        using var http = new HttpClient();
        Assert.Equal(WaveTestAudio.Create(), await http.GetByteArrayAsync(link.RootElement.GetProperty("url").GetString()));
        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, link.RootElement.GetProperty("url").GetString());
        rangeRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(44, 99);
        using var rangeResponse = await http.SendAsync(rangeRequest);
        Assert.Equal(HttpStatusCode.PartialContent, rangeResponse.StatusCode);
        Assert.Equal(WaveTestAudio.Create().AsSpan(44, 56).ToArray(), await rangeResponse.Content.ReadAsByteArrayAsync());
        using var outsider = Browser(); var stranger = await StaffAsync(outsider, RoleNames.Learner);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, AudioPath(session.Id, exercise.Id), bearer: stranger.Token)).StatusCode);
        var correct = exercise.Options.Single(item => item.Text == "Hola");
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new { sessionExerciseId = exercise.Id, selectedOptionIds = new[] { correct.Id }, idempotencyKey = Guid.NewGuid() }, student.Token)).StatusCode);
        var completed = await (await learner.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: student.Token)).Content.ReadFromJsonAsync<SessionView>();
        Assert.True(completed!.Passed);
        Assert.Equal(20, completed.AwardedXp);
        Assert.Equal(exercise.Audio, Assert.Single(completed.Results).Audio);
        Assert.Equal(exercise.Id, completed.Results[0].Id);
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Get, AudioPath(session.Id, exercise.Id), bearer: student.Token)).StatusCode);
    }

    [Fact]
    public async Task TypedListeningRejectsChoicePayloadAndSavesOnlyValidOrderedAnswers()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        Guid audio = await AudioAsync(writer.Account.Id, reader.Account.Id);
        var content = await PublishedAsync(writer.Account.Id, audio, count: 2);
        using var learner = Browser(); var student = await StaffAsync(learner, RoleNames.Learner);
        var session = await StartAsync(learner, student.Token, content);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var future = await db.SessionExercises.SingleAsync(item => item.SessionId == session.Id && item.OrderNumber == 2);
        Assert.Equal(HttpStatusCode.NotFound, (await learner.SendAsync(HttpMethod.Get, AudioPath(session.Id, future.Id), bearer: student.Token)).StatusCode);
        var invalid = await learner.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new { sessionExerciseId = session.CurrentExercise!.Id, answerText = "Hola", selectedOptionIds = new[] { Guid.NewGuid() }, idempotencyKey = Guid.NewGuid() }, student.Token);
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        Assert.Equal(0, await db.AnswerAttempts.CountAsync(item => item.UserId == student.Account.Id));
        Guid key = Guid.NewGuid();
        object answer = new { sessionExerciseId = session.CurrentExercise.Id, answerText = "hola", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = key };
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", answer, student.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", answer, student.Token)).StatusCode);
        Assert.Equal(1, await db.AnswerAttempts.CountAsync(item => item.UserId == student.Account.Id));
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Get, AudioPath(session.Id, future.Id), bearer: student.Token)).StatusCode);
        var wrong = await learner.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new { sessionExerciseId = future.Id, answerText = "wrong", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, student.Token);
        wrong.EnsureSuccessStatusCode();
        (await learner.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: student.Token)).EnsureSuccessStatusCode();
        int listening = await db.Skills.Where(item => item.CourseId == content.CourseId && item.Category == "listening")
            .Select(item => item.Id).SingleAsync();
        var request = new { content.CourseId, mistakesOnly = true, skillId = listening };
        var reviewResponse = await learner.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", request, student.Token);
        reviewResponse.EnsureSuccessStatusCode();
        var review = (await reviewResponse.Content.ReadFromJsonAsync<SessionView>())!;
        Assert.Equal("Listening", review.CurrentExercise!.Type);
        Assert.Equal(1, review.TotalCount);
        var reviewAudio = AudioPath(review.Id, review.CurrentExercise.Id);
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Get, reviewAudio, bearer: student.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.SendAsync(HttpMethod.Get, reviewAudio, bearer: reader.Token)).StatusCode);
        await db.MediaAssets.Where(item => item.Id == audio).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsVerified, false)
            .SetProperty(item => item.VerifiedBy, (Guid?)null).SetProperty(item => item.VerifiedAt, (DateTimeOffset?)null));
        Assert.Equal(HttpStatusCode.NotFound, (await learner.SendAsync(HttpMethod.Get, AudioPath(session.Id, session.CurrentExercise.Id), bearer: student.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await learner.SendAsync(HttpMethod.Get, reviewAudio, bearer: student.Token)).StatusCode);
        await db.RefreshSessions.Where(item => item.UserId == student.Account.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Unauthorized, (await learner.SendAsync(HttpMethod.Get, AudioPath(session.Id, session.CurrentExercise.Id), bearer: student.Token)).StatusCode);
    }

    [Fact]
    public async Task SavedAudioSurvivesSourceChangesAndPreventsDeletionThroughSessionReference()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        Guid oldAudio = await AudioAsync(writer.Account.Id, reader.Account.Id);
        Guid newAudio = await AudioAsync(writer.Account.Id, reader.Account.Id, source: "Replacement fixture audio");
        var content = await PublishedAsync(writer.Account.Id, oldAudio);
        using var learner = Browser(); var student = await StaffAsync(learner, RoleNames.Learner);
        var session = await StartAsync(learner, student.Token, content);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        // Isolated fixture mutation simulates source replacement; production published edits require a revision.
        await db.Exercises.Where(item => content.ExerciseIds.Contains(item.Id)).ExecuteUpdateAsync(update => update.SetProperty(item => item.AudioMediaId, newAudio));
        var resumed = await (await learner.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: student.Token)).Content.ReadFromJsonAsync<SessionView>();
        Assert.Equal("Original test recording", resumed!.CurrentExercise!.Audio!.Source);
        Assert.Equal(oldAudio, (await db.SessionExercises.SingleAsync(item => item.SessionId == session.Id)).AudioMediaId);
        Assert.Equal(HttpStatusCode.OK, (await learner.SendAsync(HttpMethod.Get, AudioPath(session.Id, session.CurrentExercise!.Id), bearer: student.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{oldAudio}", bearer: writer.Token)).StatusCode);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"MediaAssets\" WHERE \"Id\" = {oldAudio}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task DraftCannotAttachForeignUnreviewedWrongLanguageOrDeletingAudio()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var other = Browser(); var outsider = await StaffAsync(other, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var content = await DraftAsync(writer.Account.Id);
        foreach (var state in new[] { "foreign", "unreviewed", "wrong-language", "deleting" })
        {
            Guid media = await AudioAsync(state == "foreign" ? outsider.Account.Id : writer.Account.Id, reader.Account.Id);
            using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            if (state == "unreviewed" || state == "deleting")
                await db.MediaAssets.Where(item => item.Id == media).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsVerified, false)
                    .SetProperty(item => item.VerifiedBy, (Guid?)null).SetProperty(item => item.VerifiedAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.StorageState, state == "deleting" ? "Deleting" : "Ready"));
            if (state == "wrong-language") await db.MediaAssets.Where(item => item.Id == media).ExecuteUpdateAsync(update => update.SetProperty(item => item.LanguageId, 3));
            Assert.Equal(HttpStatusCode.Conflict, (await SaveAsync(author, writer.Token, content, media)).StatusCode);
        }
    }

    [Fact]
    public async Task AudioAttachmentAndAdministratorDeletionCannotBothSucceed()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        using var admin = Browser(); var manager = await StaffAsync(admin, RoleNames.Admin);
        var content = await DraftAsync(writer.Account.Id);
        Guid media = await AudioAsync(writer.Account.Id, reader.Account.Id);
        var results = await Task.WhenAll(SaveAsync(author, writer.Token, content, media),
            admin.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{media}", bearer: manager.Token));
        Assert.Single(results, item => item.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, item => item.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListeningCannotStartWithoutReviewedAudioAndValidChoiceShape()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var content = await LearningTestContent.CreateAsync(fixture.Accounts, writer.Account.Id, count: 1, type: "Listening");
        using var learner = Browser(); var student = await StaffAsync(learner, RoleNames.Learner);
        await learner.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = content.CourseId }, student.Token);
        Assert.Equal(HttpStatusCode.Conflict, (await learner.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: student.Token)).StatusCode);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        Guid media = await AudioAsync(writer.Account.Id, reader.Account.Id);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Exercises.Where(item => content.ExerciseIds.Contains(item.Id)).ExecuteUpdateAsync(update => update.SetProperty(item => item.AudioMediaId, media));
        db.ExerciseOptions.Add(new ExerciseOption { ExerciseId = content.ExerciseIds[0], Text = "Hola", OrderNumber = 1 });
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await learner.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: student.Token)).StatusCode);
    }

    private SessionTestBrowser Browser() => new(fixture.Accounts.CreateMediaClient(fixture.Storage.Store));
    private async Task<(SecurityTestAccount Account, string Token)> StaffAsync(SessionTestBrowser browser, string role)
    {
        var account = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        using var scope = fixture.Accounts.Services.CreateScope(); var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (role != RoleNames.Learner) await userManager.AddToRoleAsync((await userManager.FindByIdAsync(account.Id.ToString()))!, role);
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        db.UserLanguageScopes.Add(new UserLanguageScope { UserId = account.Id, LanguageId = 2 });
        await db.SaveChangesAsync();
        return (account, await account.LoginAsync(browser));
    }
    private async Task<Guid> AudioAsync(Guid owner, Guid reviewer, string source = "Original test recording")
    {
        Guid id = Guid.NewGuid(); var prepared = WaveAudioInspector.Inspect(WaveTestAudio.Create());
        await fixture.Storage.Store.UploadReservedAsync(id, prepared, default);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = id,
            StorageKey = $"media/{id:N}",
            MimeType = "audio/wav",
            ByteSize = prepared.Bytes.Length,
            LanguageId = 2,
            Source = source,
            License = "Original fixture audio; test use permitted",
            UploadedBy = owner,
            IsVerified = true,
            VerifiedBy = reviewer,
            VerifiedAt = DateTimeOffset.UtcNow,
            DurationMilliseconds = prepared.DurationMilliseconds,
            Sha256 = Convert.ToHexString(SHA256.HashData(prepared.Bytes.Span)).ToLowerInvariant()
        });
        await db.SaveChangesAsync(); return id;
    }
    private async Task<LearningTestContent> DraftAsync(Guid owner)
    {
        var content = await LearningTestContent.CreateAsync(fixture.Accounts, owner, count: 1, type: "Listening");
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.LessonVersions.Where(item => item.Id == content.VersionId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Draft"));
        await db.Lessons.Where(item => item.Id == content.LessonId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Draft").SetProperty(item => item.CurrentVersionId, (Guid?)null));
        return content;
    }
    private async Task<LearningTestContent> PublishedAsync(Guid owner, Guid audio, int count = 1)
    {
        var content = await LearningTestContent.CreateAsync(fixture.Accounts, owner, count: count, type: "Listening");
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Exercises.Where(item => content.ExerciseIds.Contains(item.Id)).ExecuteUpdateAsync(update => update.SetProperty(item => item.AudioMediaId, audio));
        return content;
    }
    private static Task<HttpResponseMessage> SaveAsync(SessionTestBrowser browser, string token, LearningTestContent content, Guid media, string[]? options = null) =>
        browser.SendAsync(HttpMethod.Put, $"/api/v1/manage/lessons/{content.LessonId}/exercises/{content.ExerciseIds[0]}",
            new
            {
                versionId = content.VersionId,
                type = "Listening",
                prompt = "Listen, then answer.",
                explanation = "Hola is a greeting.",
                orderNumber = 1,
                options = options ?? [],
                answers = new[] { "Hola" },
                audioMediaId = media
            }, token);
    private static string AudioPath(Guid session, Guid exercise) => $"/api/v1/me/learning-sessions/{session}/exercises/{exercise}/audio";
    private static async Task<SessionView> StartAsync(SessionTestBrowser browser, string token, LearningTestContent content)
    {
        using var enrollment = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments",
            new { courseId = content.CourseId }, token);
        Assert.Equal(HttpStatusCode.Created, enrollment.StatusCode);
        Assert.NotNull(enrollment.Headers.Location);
        var response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SessionView>())!;
        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"/api/v1/me/learning-sessions/{session.Id}", response.Headers.Location!.AbsolutePath);
        using var detail = await browser.SendAsync(HttpMethod.Get, response.Headers.Location.PathAndQuery, bearer: token);
        detail.EnsureSuccessStatusCode();
        Assert.Equal(session.Id, (await detail.Content.ReadFromJsonAsync<SessionView>())!.Id);
        return session;
    }
}
