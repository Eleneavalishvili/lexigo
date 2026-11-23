using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Infrastructure.Storage;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Storage;

public sealed class MediaWorkflowTests(MediaWorkflowFixture fixture) : IClassFixture<MediaWorkflowFixture>
{
    [Fact]
    public async Task ValidUploadHasPrivateCanonicalBytesAndIndependentScopedReview()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        byte[] input = WaveTestAudio.Create(metadata: true, extendedFormat: true);
        Guid id = await UploadAsync(author, writer.Token, input);
        using var guest = Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.SendAsync(HttpMethod.Get, $"/api/v1/manage/media/{id}")).StatusCode);
        using var stranger = Browser();
        var another = await StaffAsync(stranger, RoleNames.ContentAuthor);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.SendAsync(HttpMethod.Get,
            $"/api/v1/manage/media/{id}", bearer: another.Token)).StatusCode);
        var rows = await (await author.SendAsync(HttpMethod.Get, "/api/v1/manage/media", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffMedia[]>();
        var item = Assert.Single(rows!, item => item.Id == id);
        Assert.False(item.IsVerified);
        Assert.Equal(100, item.DurationMilliseconds);
        Assert.Equal(WaveTestAudio.Create().Length, item.ByteSize);
        using var scope = fixture.Accounts.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var stored = await db.MediaAssets.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal($"media/{id:N}", stored.StorageKey);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(WaveTestAudio.Create())).ToLowerInvariant(), stored.Sha256);
        Assert.Null(stored.VerifiedBy);
        var preview = await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/media/{id}/preview", bearer: writer.Token);
        Assert.True(preview.Headers.CacheControl?.NoStore);
        using var link = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        using var http = new HttpClient();
        Assert.Equal(WaveTestAudio.Create(), await http.GetByteArrayAsync(link.RootElement.GetProperty("url").GetString()));
        using var other = Browser();
        var outsider = await StaffAsync(other, RoleNames.ContentAuthor);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Get, $"/api/v1/manage/media/{id}/preview", bearer: outsider.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{id}", bearer: outsider.Token)).StatusCode);
        using var reviewer = Browser();
        var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/media/{id}/reviews", new { decision = "approve", comment = "My own recording." }, writer.Token)).StatusCode);
        var reviews = await Task.WhenAll(ReviewAsync(reviewer, reader.Token, id), ReviewAsync(reviewer, reader.Token, id));
        Assert.Single(reviews, result => result.StatusCode == HttpStatusCode.OK);
        Assert.Single(reviews, result => result.StatusCode == HttpStatusCode.Conflict);
        var verified = await db.MediaAssets.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.True(verified.IsVerified);
        Assert.Equal(reader.Account.Id, verified.VerifiedBy);
        Assert.NotNull(verified.VerifiedAt);
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.EntityId == id.ToString() && item.Action == "MediaApproved"));
        var reviewedRows = await (await author.SendAsync(HttpMethod.Get, "/api/v1/manage/media", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffMedia[]>();
        Assert.Equal("Checked the recording and permission statement.", Assert.Single(reviewedRows!, item => item.Id == id).ReviewComment);
        await DeleteAndCleanAsync(author, writer.Token, id);
    }

    [Fact]
    public async Task MalformedAudioAndMissingRightsNeverCreateReservations()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var scope = fixture.Accounts.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        foreach (var bytes in new[] { "<script>not audio</script>"u8.ToArray(), WaveTestAudio.Create()[..100] })
        {
            var response = await author.SendAsync(HttpMethod.Post, "/api/v1/manage/media", Form(bytes), writer.Token);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await author.SendAsync(HttpMethod.Post, "/api/v1/manage/media", Form(WaveTestAudio.Create(), source: ""), writer.Token)).StatusCode);
        Assert.Equal(0, await db.MediaAssets.CountAsync(item => item.UploadedBy == writer.Account.Id));
    }

    [Fact]
    public async Task GuestLearnerReviewerAndOutOfScopeAuthorCannotUpload()
    {
        using var guest = Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.SendAsync(HttpMethod.Post, "/api/v1/manage/media", Form(WaveTestAudio.Create()))).StatusCode);
        foreach (var role in new[] { RoleNames.Learner, RoleNames.Reviewer, RoleNames.ContentAuthor })
        {
            using var user = Browser();
            var staff = await StaffAsync(user, role, language: 3);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.SendAsync(HttpMethod.Post, "/api/v1/manage/media", Form(WaveTestAudio.Create()), staff.Token)).StatusCode);
        }
    }

    [Fact]
    public async Task ReviewerCannotApproveOwnAudioEvenWithBothRolesOrWrongLanguage()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor, additionalRole: RoleNames.Reviewer);
        Guid id = await UploadAsync(author, writer.Token, WaveTestAudio.Create());
        Assert.Equal(HttpStatusCode.Forbidden, (await ReviewAsync(author, writer.Token, id)).StatusCode);
        using var wrongLanguage = Browser(); var reviewer = await StaffAsync(wrongLanguage, RoleNames.Reviewer, language: 3);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReviewAsync(wrongLanguage, reviewer.Token, id)).StatusCode);
        await DeleteAndCleanAsync(author, writer.Token, id);
    }

    [Fact]
    public async Task DeleteIsIdempotentAndStorageFailureRemainsDurableUntilRetry()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid id = await UploadAsync(author, writer.Token, WaveTestAudio.Create());
        var deletes = await Task.WhenAll(author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{id}", bearer: writer.Token),
            author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{id}", bearer: writer.Token));
        Assert.All(deletes, item => Assert.Equal(HttpStatusCode.OK, item.StatusCode));
        Assert.Equal(HttpStatusCode.NotFound, (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/media/{id}/preview", bearer: writer.Token)).StatusCode);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.EntityId == id.ToString() && item.Action == "MediaDeletionRequested"));
        var failing = new MediaCleanupService(db, new Lazy<MediaObjectStore>(() => throw new IOException("Synthetic storage outage")), TimeProvider.System);
        await Assert.ThrowsAsync<IOException>(() => failing.ProcessNextAsync(default));
        Assert.Equal("Deleting", (await db.MediaAssets.AsNoTracking().SingleAsync(item => item.Id == id)).StorageState);
        var cleanup = await Task.WhenAll(CleanupAsync(), CleanupAsync());
        Assert.Single(cleanup, result => result);
        Assert.False(await db.MediaAssets.AsNoTracking().AnyAsync(item => item.Id == id));
        var link = await fixture.Storage.Store.CreateReadLinkAsync($"media/{id:N}", TimeSpan.FromMinutes(1));
        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(link)).StatusCode);
    }

    [Fact]
    public async Task StaffCanFinishQueuedDeletionWithSafeRetriesButCannotRemoveAnotherAuthorsFile()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var other = Browser();
        var outsider = await StaffAsync(other, RoleNames.ContentAuthor);
        Guid id = await UploadAsync(author, writer.Token, WaveTestAudio.Create());
        string path = $"/api/v1/manage/media/{id}/finish-deletion";
        Assert.Equal(HttpStatusCode.Conflict, (await author.SendAsync(HttpMethod.Post, path, new { }, writer.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.SendAsync(HttpMethod.Post, path, new { }, outsider.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{id}", bearer: writer.Token)).StatusCode);

        using var disabled = new SessionTestBrowser(fixture.Accounts.CreateIsolatedClient());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await disabled.SendAsync(HttpMethod.Post, path, new { }, writer.Token)).StatusCode);
        using var scope = fixture.Accounts.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal("Deleting", (await db.MediaAssets.AsNoTracking().SingleAsync(item => item.Id == id)).StorageState);

        var results = await Task.WhenAll(
            author.SendAsync(HttpMethod.Post, path, new { }, writer.Token),
            author.SendAsync(HttpMethod.Post, path, new { }, writer.Token));
        Assert.All(results, result => Assert.Equal(HttpStatusCode.OK, result.StatusCode));
        Assert.False(await db.MediaAssets.AnyAsync(item => item.Id == id));
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.EntityId == id.ToString() && item.Action == "MediaDeletionCompleted"));
        Assert.Equal(HttpStatusCode.Forbidden, (await other.SendAsync(HttpMethod.Post, path, new { }, outsider.Token)).StatusCode);
        var link = await fixture.Storage.Store.CreateReadLinkAsync($"media/{id:N}", TimeSpan.FromMinutes(1));
        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(link)).StatusCode);
    }

    [Fact]
    public async Task FinishDeletionRequiresCurrentStaffPermissionAndAllowsAdministratorCleanup()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid id = await UploadAsync(author, writer.Token, WaveTestAudio.Create());
        Assert.Equal(HttpStatusCode.OK, (await author.SendAsync(HttpMethod.Delete,
            $"/api/v1/manage/media/{id}", bearer: writer.Token)).StatusCode);
        string path = $"/api/v1/manage/media/{id}/finish-deletion";
        using var guest = Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.SendAsync(HttpMethod.Post, path, new { })).StatusCode);
        foreach (string role in new[] { RoleNames.Learner, RoleNames.Reviewer })
        {
            using var browser = Browser();
            var account = await StaffAsync(browser, role);
            Assert.Equal(HttpStatusCode.Forbidden, (await browser.SendAsync(HttpMethod.Post, path, new { }, account.Token)).StatusCode);
        }
        using var scope = fixture.Accounts.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.UserLanguageScopes.Where(item => item.UserId == writer.Account.Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await author.SendAsync(HttpMethod.Post, path, new { }, writer.Token)).StatusCode);
        await db.RefreshSessions.Where(item => item.UserId == writer.Account.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Unauthorized, (await author.SendAsync(HttpMethod.Post, path, new { }, writer.Token)).StatusCode);
        using var administrator = Browser();
        var admin = await StaffAsync(administrator, RoleNames.Admin);
        Assert.Equal(HttpStatusCode.OK, (await administrator.SendAsync(HttpMethod.Post, path, new { }, admin.Token)).StatusCode);
        Assert.False(await db.MediaAssets.AnyAsync(item => item.Id == id));
    }

    [Fact]
    public async Task CompletedRecordingCleanupUnblocksSyntheticAuthorAccountDeletion()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid id = await UploadAsync(author, writer.Token, WaveTestAudio.Create());
        var confirmation = new
        {
            currentPassword = SecurityTestAccount.Password,
            confirmation = "DELETE",
            recoveryCode = writer.Account.RecoveryCodes[1]
        };
        var blocked = await author.SendAsync(HttpMethod.Delete, "/api/v1/me", confirmation, writer.Token, csrf: true);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("unused uploaded recordings", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await author.SendAsync(HttpMethod.Delete,
            $"/api/v1/manage/media/{id}", bearer: writer.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await author.SendAsync(HttpMethod.Post,
            $"/api/v1/manage/media/{id}/finish-deletion", new { }, writer.Token)).StatusCode);
        var deleted = await author.SendAsync(HttpMethod.Delete, "/api/v1/me", confirmation, writer.Token, csrf: true);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var scope = fixture.Accounts.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal("Deleted", await db.Users.Where(item => item.Id == writer.Account.Id).Select(item => item.Status).SingleAsync());
    }

    [Fact]
    public async Task OldInterruptedUploadsAreCleanedButReadyAndRecentPendingFilesAreKept()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid id = await UploadAsync(author, writer.Token, WaveTestAudio.Create());
        Assert.False(await CleanupAsync());
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.MediaAssets.Where(item => item.Id == id).ExecuteUpdateAsync(update => update.SetProperty(item => item.StorageState, "Pending"));
        Assert.False(await CleanupAsync());
        await db.MediaAssets.Where(item => item.Id == id).ExecuteUpdateAsync(update => update.SetProperty(item => item.CreatedAt, DateTimeOffset.UtcNow.AddHours(-2)));
        Assert.True(await CleanupAsync());
        Assert.False(await db.MediaAssets.AnyAsync(item => item.Id == id));
    }

    [Fact]
    public async Task ReferencedMediaCannotBeDeletedAndDatabaseRejectsSelfReview()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid id = await UploadAsync(author, writer.Token, WaveTestAudio.Create());
        var content = await LearningTestContent.CreateAsync(fixture.Accounts, writer.Account.Id);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Exercises.Where(item => item.Id == content.ExerciseIds[0]).ExecuteUpdateAsync(update => update.SetProperty(item => item.AudioMediaId, id));
        await db.MediaAssets.Where(item => item.Id == id).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsVerified, true));
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<LessonValidation>().ValidateAsync(content.VersionId, default));
        await db.MediaAssets.Where(item => item.Id == id).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsVerified, false));
        Assert.Equal(HttpStatusCode.Conflict, (await author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{id}", bearer: writer.Token)).StatusCode);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"MediaAssets\" SET \"IsVerified\" = true, \"VerifiedBy\" = {writer.Account.Id}, \"VerifiedAt\" = CURRENT_TIMESTAMP WHERE \"Id\" = {id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        await db.Exercises.Where(item => item.Id == content.ExerciseIds[0]).ExecuteUpdateAsync(update => update.SetProperty(item => item.AudioMediaId, (Guid?)null));
        await DeleteAndCleanAsync(author, writer.Token, id);
    }

    [Fact]
    public async Task DisabledStorageAndRevokedSessionsDoNotAcceptUploads()
    {
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var disabled = new SessionTestBrowser(fixture.Accounts.CreateIsolatedClient());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await disabled.SendAsync(HttpMethod.Post, "/api/v1/manage/media", Form(WaveTestAudio.Create()), writer.Token)).StatusCode);
        using var scope = fixture.Accounts.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.RefreshSessions.Where(item => item.UserId == writer.Account.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Unauthorized, (await author.SendAsync(HttpMethod.Post, "/api/v1/manage/media", Form(WaveTestAudio.Create()), writer.Token)).StatusCode);
    }

    private SessionTestBrowser Browser() => new(fixture.Accounts.CreateMediaClient(fixture.Storage.Store));

    private async Task<(SecurityTestAccount Account, string Token)> StaffAsync(SessionTestBrowser browser, string role, int language = 2, string? additionalRole = null)
    {
        var account = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        using var scope = fixture.Accounts.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(account.Id.ToString()))!;
        if (role != RoleNames.Learner) await users.AddToRoleAsync(user, role);
        if (additionalRole is not null) await users.AddToRoleAsync(user, additionalRole);
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        db.UserLanguageScopes.Add(new UserLanguageScope { UserId = account.Id, LanguageId = language });
        await db.SaveChangesAsync();
        return (account, await account.LoginAsync(browser));
    }

    private static MultipartFormDataContent Form(byte[] bytes, string source = "Original recording by test author; isolated fixture only")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "untrusted-filename.wav");
        form.Add(new StringContent("2"), "languageId");
        form.Add(new StringContent(source), "source");
        form.Add(new StringContent("Original work; permission granted for test use"), "license");
        return form;
    }

    private static async Task<Guid> UploadAsync(SessionTestBrowser browser, string token, byte[] bytes)
    {
        var response = await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/media", Form(bytes), token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid id = document.RootElement.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/manage/media/{id}", response.Headers.Location!.AbsolutePath);
        using var detail = await browser.SendAsync(HttpMethod.Get, response.Headers.Location.PathAndQuery, bearer: token);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.True(detail.Headers.CacheControl?.NoStore);
        using var metadata = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal(id, metadata.RootElement.GetProperty("id").GetGuid());
        Assert.False(metadata.RootElement.TryGetProperty("storageKey", out _));
        return id;
    }

    private static Task<HttpResponseMessage> ReviewAsync(SessionTestBrowser browser, string token, Guid id) =>
        browser.SendAsync(HttpMethod.Post, $"/api/v1/manage/media/{id}/reviews", new { decision = "approve", comment = "Checked the recording and permission statement." }, token);

    private async Task<bool> CleanupAsync()
    {
        using var scope = fixture.Accounts.Services.CreateScope();
        return await new MediaCleanupService(scope.ServiceProvider.GetRequiredService<LexiGoDbContext>(),
            new Lazy<MediaObjectStore>(() => fixture.Storage.Store), TimeProvider.System).ProcessNextAsync(default);
    }

    private async Task DeleteAndCleanAsync(SessionTestBrowser browser, string token, Guid id)
    {
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Delete, $"/api/v1/manage/media/{id}", bearer: token)).StatusCode);
        Assert.True(await CleanupAsync());
    }
}
