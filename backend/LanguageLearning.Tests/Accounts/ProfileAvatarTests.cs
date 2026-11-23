using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Storage;
using LanguageLearning.Tests.Learning;
using LanguageLearning.Tests.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Accounts;

public sealed class ProfileAvatarTests(MediaWorkflowFixture fixture) : IClassFixture<MediaWorkflowFixture>
{
    [Fact]
    public async Task AvatarIsPrivateAndReplacementQueuesThePreviousObjectForCleanup()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        using var browser = new SessionTestBrowser(fixture.Accounts.CreateMediaClient(fixture.Storage.Store));
        string token = await account.LoginAsync(browser);
        const string route = "/api/v1/me/avatar";
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, route)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await browser.SendAsync(HttpMethod.Put, route, Pixels(1), token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await browser.SendAsync(HttpMethod.Put, route, Pixels(AvatarPng.PixelBytes), token)).StatusCode);

        var response = await browser.SendAsync(HttpMethod.Get, route, bearer: token);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl!.NoStore);
        var link = (await response.Content.ReadFromJsonAsync<AvatarLink>())!;
        using var http = new HttpClient();
        using var file = await http.GetAsync(link.Url);
        file.EnsureSuccessStatusCode();
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);

        var other = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        using var outsider = new SessionTestBrowser(fixture.Accounts.CreateMediaClient(fixture.Storage.Store));
        Assert.Equal(HttpStatusCode.NoContent, (await outsider.SendAsync(HttpMethod.Get, route,
            bearer: await other.LoginAsync(outsider))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await browser.SendAsync(HttpMethod.Put, route, Pixels(AvatarPng.PixelBytes), token)).StatusCode);
        using var scope = fixture.Accounts.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var media = await db.MediaAssets.Where(item => item.UploadedBy == account.Id).ToArrayAsync();
        var active = Assert.Single(media, item => item.StorageState == "Ready");
        Assert.Single(media, item => item.StorageState == "Deleting");
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => db.Users.Where(user => user.Id == other.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.AvatarMediaId, (Guid?)active.Id)));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, rejected.SqlState);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Delete, route, bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Get, route, bearer: token)).StatusCode);

        var cleanup = new MediaCleanupService(db, new Lazy<MediaObjectStore>(() => fixture.Storage.Store), TimeProvider.System);
        Assert.True(await cleanup.ProcessNextAsync(CancellationToken.None));
        Assert.True(await cleanup.ProcessNextAsync(CancellationToken.None));
        Assert.False(await db.MediaAssets.AnyAsync(item => item.UploadedBy == account.Id));
        using var deletedFile = await http.GetAsync(link.Url);
        Assert.Equal(HttpStatusCode.NotFound, deletedFile.StatusCode);
    }

    [Fact]
    public async Task SharedRankingPhotoIsAvailableToCourseMembersUntilConsentIsWithdrawn()
    {
        var author = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        var viewer = await SecurityTestAccount.CreateAsync(fixture.Accounts);
        using var ownerBrowser = new SessionTestBrowser(fixture.Accounts.CreateMediaClient(fixture.Storage.Store));
        using var viewerBrowser = new SessionTestBrowser(fixture.Accounts.CreateMediaClient(fixture.Storage.Store));
        string ownerToken = await author.LoginAsync(ownerBrowser);
        string viewerToken = await viewer.LoginAsync(viewerBrowser);
        var content = await LearningTestContent.CreateAsync(fixture.Accounts, author.Id);
        var board = $"/api/v1/leaderboards?courseId={content.CourseId}";
        const string consent = "/api/v1/me/leaderboard-participation";
        const string enrollments = "/api/v1/me/enrollments";

        (await ownerBrowser.SendAsync(HttpMethod.Post, enrollments, new { content.CourseId }, ownerToken))
            .EnsureSuccessStatusCode();
        (await ownerBrowser.SendAsync(HttpMethod.Put, "/api/v1/me/avatar", Pixels(AvatarPng.PixelBytes), ownerToken))
            .EnsureSuccessStatusCode();
        (await ownerBrowser.SendAsync(HttpMethod.Put, consent, new { optedIn = true }, ownerToken))
            .EnsureSuccessStatusCode();
        await using (var scope = fixture.Accounts.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.XpTransactions.Add(new()
            {
                UserId = author.Id, CourseId = content.CourseId, SourceType = "LeaderboardFixture",
                SourceId = Guid.NewGuid(), Amount = 20, EarnedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await viewerBrowser.SendAsync(HttpMethod.Get, board)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await viewerBrowser.SendAsync(HttpMethod.Get, board, bearer: viewerToken)).StatusCode);
        (await viewerBrowser.SendAsync(HttpMethod.Post, enrollments, new { content.CourseId }, viewerToken))
            .EnsureSuccessStatusCode();
        async Task<LeaderboardEntry> ReadRow()
        {
            using var response = await viewerBrowser.SendAsync(HttpMethod.Get, board, bearer: viewerToken);
            response.EnsureSuccessStatusCode();
            Assert.True(response.Headers.CacheControl!.NoStore);
            return Assert.Single((await response.Content.ReadFromJsonAsync<LeaderboardView>())!.Items);
        }
        Assert.Null((await ReadRow()).PhotoUrl);
        (await ownerBrowser.SendAsync(HttpMethod.Put, consent, new { optedIn = true, sharePhoto = true }, ownerToken))
            .EnsureSuccessStatusCode();
        var shared = (await ReadRow()).PhotoUrl;
        Assert.NotNull(shared);
        using var http = new HttpClient();
        using var image = await http.GetAsync(shared);
        image.EnsureSuccessStatusCode();
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        (await ownerBrowser.SendAsync(HttpMethod.Put, consent, new { optedIn = true, sharePhoto = false }, ownerToken))
            .EnsureSuccessStatusCode();
        Assert.Null((await ReadRow()).PhotoUrl);

        (await ownerBrowser.SendAsync(HttpMethod.Put, consent, new { optedIn = true, sharePhoto = true }, ownerToken))
            .EnsureSuccessStatusCode();
        await using var mediaScope = fixture.Accounts.Services.CreateAsyncScope();
        var database = mediaScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await database.MediaAssets.Where(item => item.UploadedBy == author.Id)
            .ExecuteUpdateAsync(change => change.SetProperty(item => item.StorageKey, "invalid-legacy-avatar-key"));
        var entry = await ReadRow();
        Assert.Null(entry.PhotoUrl);
        Assert.Equal(20, entry.Xp);
    }

    private static ByteArrayContent Pixels(int length)
    {
        var content = new ByteArrayContent(new byte[length]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private sealed record AvatarLink(string Url);
}
