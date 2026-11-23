using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace LanguageLearning.Tests.Storage;

public sealed class MediaObjectStoreTests(StorageFixture fixture) : IClassFixture<StorageFixture>
{
    [Fact]
    public async Task PrivateObjectSupportsSignedPlaybackAndDeletion()
    {
        byte[] audio = [82, 73, 70, 70, 1, 2, 3, 4];
        using var stream = new MemoryStream(audio);
        var key = await fixture.Store.UploadAsync(stream, "audio/wav");
        Assert.True(stream.CanRead);
        Assert.Matches("^media/[a-f0-9]{32}$", key);
        using var http = new HttpClient();
        using var anonymous = await http.GetAsync($"{fixture.Endpoint}/{fixture.Bucket}/{key}");
        Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);
        var link = await fixture.Store.CreateReadLinkAsync(key, TimeSpan.FromMinutes(1));
        using var playback = await http.GetAsync(link);
        Assert.Equal(HttpStatusCode.OK, playback.StatusCode);
        Assert.Equal(audio, await playback.Content.ReadAsByteArrayAsync());
        Assert.Equal("audio/wav", playback.Content.Headers.ContentType?.MediaType);
        Assert.True(playback.Headers.CacheControl?.NoStore);
        var metadata = await fixture.Client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = fixture.Bucket, Key = key });
        Assert.Equal(audio.Length, metadata.ContentLength);
        await fixture.Store.DeleteAsync(key);
        using var missing = await http.GetAsync(link);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ExpiredPlaybackLinkIsDenied()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var key = await fixture.Store.UploadAsync(stream, "audio/ogg");
        try
        {
            var link = await fixture.Store.CreateReadLinkAsync(key, TimeSpan.FromSeconds(5));
            using var http = new HttpClient();
            using var initial = await http.GetAsync(link);
            Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
            await Task.Delay(TimeSpan.FromSeconds(7));
            using var response = await http.GetAsync(link);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = System.Xml.Linq.XDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("InvalidRequest", error.Root?.Element("Code")?.Value);
        }
        finally { await fixture.Store.DeleteAsync(key); }
    }

    [Fact]
    public async Task TamperedSignatureCannotReadPrivateObject()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var key = await fixture.Store.UploadAsync(stream, "audio/ogg");
        try
        {
            var link = await fixture.Store.CreateReadLinkAsync(key, TimeSpan.FromMinutes(1));
            int signature = link.IndexOf("X-Amz-Signature=", StringComparison.Ordinal) + "X-Amz-Signature=".Length;
            Assert.True(signature >= "X-Amz-Signature=".Length);
            var tampered = link[..signature] + (link[signature] == '0' ? '1' : '0') + link[(signature + 1)..];
            using var http = new HttpClient();
            using var response = await http.GetAsync(tampered);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally { await fixture.Store.DeleteAsync(key); }
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("media/not-a-guid")]
    [InlineData("other/00000000000000000000000000000000")]
    public async Task ForeignOrMalformedKeysAreRejectedBeforeStorageAccess(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.CreateReadLinkAsync(key, TimeSpan.FromMinutes(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.DeleteAsync(key));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    public async Task ReadLinksHaveStrictLifetimeBounds(int seconds)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Store.CreateReadLinkAsync(
            $"media/{Guid.NewGuid():N}", TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public async Task UploadRejectsEmptyOversizedAndUnsupportedInputs()
    {
        using var empty = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.UploadAsync(empty, "audio/wav"));
        using var oversized = new MemoryStream(new byte[Infrastructure.Storage.MediaObjectStore.MaximumBytes + 1]);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.UploadAsync(oversized, "audio/wav"));
        using var html = new MemoryStream([1]);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.UploadAsync(html, "text/html"));
        html.Position = 1;
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.UploadAsync(html, "audio/wav"));
    }
}
