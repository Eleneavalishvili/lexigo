using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Storage;

public sealed class MediaCleanupService(LexiGoDbContext database, Lazy<MediaObjectStore> store, TimeProvider clock)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var threshold = clock.GetUtcNow().AddHours(-1);
        var rows = await database.MediaAssets.FromSqlInterpolated($"SELECT * FROM \"MediaAssets\" WHERE \"StorageState\" = 'Deleting' OR (\"StorageState\" = 'Pending' AND \"CreatedAt\" < {threshold}) ORDER BY \"CreatedAt\" LIMIT 1 FOR UPDATE SKIP LOCKED")
            .ToArrayAsync(cancellationToken);
        var media = rows.SingleOrDefault();
        if (media is null) return false;
        if (!await RemoveLockedAsync(media, null, "media-cleanup", cancellationToken))
            throw new InvalidOperationException("Referenced media cannot enter storage cleanup.");
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // The caller owns a transaction and has locked this media row.
    internal async Task<bool> RemoveLockedAsync(MediaAsset media, Guid? actorId, string traceId, CancellationToken cancellationToken)
    {
        if (await database.Users.AnyAsync(item => item.AvatarMediaId == media.Id, cancellationToken)
            || await database.Exercises.AnyAsync(item => item.AudioMediaId == media.Id, cancellationToken)
            || await database.SessionExercises.AnyAsync(item => item.AudioMediaId == media.Id, cancellationToken)
            || await database.ExerciseOptions.AnyAsync(item => item.AudioMediaId == media.Id, cancellationToken)
            || await database.SessionOptionAudio.AnyAsync(item => item.AudioMediaId == media.Id, cancellationToken)
            || await database.DialogueNodes.AnyAsync(item => item.AudioMediaId == media.Id, cancellationToken)
            || await database.VocabularyItems.AnyAsync(item => item.AudioMediaId == media.Id, cancellationToken))
            return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await store.Value.DeleteAsync(media.StorageKey, timeout.Token);
        database.MediaAssets.Remove(media);
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            EntityType = "MediaAsset",
            EntityId = media.Id.ToString(),
            Action = "MediaDeletionCompleted",
            Reason = "Unused file removed from private storage.",
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }
}
