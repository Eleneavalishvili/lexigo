using System.Security.Cryptography;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Content;

public sealed class MediaUploadService(LexiGoDbContext database, ContentAccess access, Lazy<MediaObjectStore> store,
    IOptions<ObjectStorageOptions> options, TimeProvider clock, ILogger<MediaUploadService> logger,
    SystemSettings settings)
{
    public async Task<MediaOutcome> UploadAsync(Guid actorId, Guid familyId, MediaUploadInput input, Stream inputStream,
        string traceId, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        if (actor is null || !actor.CanAuthor(input.LanguageId)) return new(Forbidden: true);
        if (!ValidText(input.Source, 2000) || !ValidText(input.License, 1000))
            return new(Error: "Provide the audio source and your permission or license to use it.");
        if (!options.Value.Enabled) return new(Unavailable: true);
        PreparedAudio audio;
        int limit = (await settings.ReadAsync(cancellationToken)).Defaults.AudioUploadMiB * 1024 * 1024;
        try { audio = await WaveAudioInspector.ReadAsync(inputStream, cancellationToken, limit); }
        catch (InvalidDataException error) { return new(Error: error.Message); }

        var media = new MediaAsset
        {
            StorageKey = "",
            MimeType = audio.MimeType,
            ByteSize = audio.Bytes.Length,
            LanguageId = input.LanguageId,
            Source = input.Source.Trim(),
            License = input.License.Trim(),
            UploadedBy = actorId,
            StorageState = "Pending",
            CreatedAt = clock.GetUtcNow(),
            DurationMilliseconds = audio.DurationMilliseconds,
            Sha256 = Convert.ToHexString(SHA256.HashData(audio.Bytes.Span)).ToLowerInvariant()
        };
        media.StorageKey = $"media/{media.Id:N}";
        await using (var reservation = await database.Database.BeginTransactionAsync(cancellationToken))
        {
            actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
            if (actor is null || !actor.CanAuthor(input.LanguageId)) return new(Forbidden: true);
            if (!await database.Languages.AnyAsync(item => item.Id == input.LanguageId, cancellationToken))
                return new(Error: "Choose a supported language.");
            long used = await database.MediaAssets.Where(item => item.UploadedBy == actorId).SumAsync(item => (long?)item.ByteSize, cancellationToken) ?? 0;
            if (used + media.ByteSize > 200L * 1024 * 1024) return new(Error: "Your audio library has reached its 200 MiB limit. Remove unused uploads first.");
            database.MediaAssets.Add(media);
            await database.SaveChangesAsync(cancellationToken);
            await reservation.CommitAsync(cancellationToken);
        }

        // This durable reservation lets cleanup find bytes even if the process stops after the PUT.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null || !actor.CanAuthor(input.LanguageId)) return new(Forbidden: true);
        _ = await database.MediaAssets.FromSqlInterpolated($"SELECT * FROM \"MediaAssets\" WHERE \"Id\" = {media.Id} FOR UPDATE").ToArrayAsync(cancellationToken);
        await database.Entry(media).ReloadAsync(cancellationToken);
        if (media.StorageState != "Pending") return new(Error: "This upload is no longer available.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await store.Value.UploadReservedAsync(media.Id, audio, timeout.Token);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Media upload {MediaId} was interrupted; its private reservation remains available for cleanup.", media.Id);
            return new(Unavailable: true);
        }
        media.StorageState = "Ready";
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            EntityType = "MediaAsset",
            EntityId = media.Id.ToString(),
            Action = "MediaUploaded",
            Reason = "Validated PCM audio uploaded for independent review.",
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(media.Id);
    }

    private static bool ValidText(string value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && !value.Contains('\0');
}
