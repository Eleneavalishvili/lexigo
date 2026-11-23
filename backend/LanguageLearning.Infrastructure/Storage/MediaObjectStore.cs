using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Storage;

// Internal storage boundary. API services must establish ownership, licensing and file validity first.
public sealed class MediaObjectStore(IAmazonS3 client, IOptions<ObjectStorageOptions> options, TimeProvider clock)
{
    public const long MaximumBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AudioTypes = ["audio/mpeg", "audio/ogg", "audio/wav", "audio/webm", "audio/mp4"];

    public async Task<string> UploadAsync(PreparedAudio audio, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(audio.Bytes.ToArray(), writable: false);
        return await UploadAsync(stream, audio.MimeType, cancellationToken);
    }

    // The application reserves this server-generated ID in SQL before writing any bytes.
    public async Task UploadReservedAsync(Guid mediaId, PreparedAudio audio, CancellationToken cancellationToken)
    {
        await UploadReservedAsync(mediaId, audio.Bytes, audio.MimeType, cancellationToken);
    }

    internal Task UploadAvatarAsync(Guid mediaId, byte[] png, CancellationToken cancellationToken) =>
        UploadReservedAsync(mediaId, png, "image/png", cancellationToken);

    private async Task UploadReservedAsync(Guid mediaId, ReadOnlyMemory<byte> bytes, string contentType,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.Bucket,
            Key = $"media/{mediaId:N}",
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
            UseChunkEncoding = false
        }, cancellationToken);
    }

    public async Task<string> UploadAsync(Stream audio, string contentType, CancellationToken cancellationToken = default)
    {
        if (!audio.CanRead || !audio.CanSeek || audio.Position != 0 || audio.Length is < 1 or > MaximumBytes)
            throw new ArgumentException("Audio must be a readable, bounded stream positioned at its beginning.", nameof(audio));
        if (!AudioTypes.Contains(contentType)) throw new ArgumentException("Unsupported audio content type.", nameof(contentType));

        // Callers cannot choose an existing key or overwrite a reviewed object's bytes.
        string key = $"media/{Guid.NewGuid():N}";
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.Bucket,
            Key = key,
            InputStream = audio,
            ContentType = contentType,
            AutoCloseStream = false,
            UseChunkEncoding = false
        }, cancellationToken);
        return key;
    }

    public Task<string> CreateReadLinkAsync(string key, TimeSpan lifetime)
    {
        ValidateKey(key);
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Playback links must expire within five minutes.");
        return client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = options.Value.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = new Uri(options.Value.ServiceUrl).Scheme == "https" ? Protocol.HTTPS : Protocol.HTTP,
            Expires = clock.GetUtcNow().UtcDateTime.Add(lifetime),
            ResponseHeaderOverrides = new ResponseHeaderOverrides { CacheControl = "private, no-store" }
        });
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        return client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = options.Value.Bucket, Key = key }, cancellationToken);
    }

    private static void ValidateKey(string key)
    {
        if (key.Length != 38 || !key.StartsWith("media/", StringComparison.Ordinal)
            || !Guid.TryParseExact(key.AsSpan(6), "N", out _))
            throw new ArgumentException("Invalid media object key.", nameof(key));
    }
}
