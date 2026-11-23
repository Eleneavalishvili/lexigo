using Amazon.S3;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Content;

public sealed class MediaManagementService(LexiGoDbContext database, ContentAccess access, TimeProvider clock,
    MediaCleanupService cleanup, IOptions<ObjectStorageOptions> options)
{
    public async Task<MediaOutcome> FinishDeletionAsync(Guid actorId, Guid familyId, Guid mediaId, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null) return new(Forbidden: true);
        var media = await LockAsync(mediaId, cancellationToken);
        if (media is null)
        {
            bool requested = actor.IsAdmin || await database.AuditLogs.AnyAsync(item =>
                item.ActorId == actorId && item.EntityType == "MediaAsset" && item.EntityId == mediaId.ToString()
                && (item.Action == "MediaDeletionRequested" || item.Action == "MediaDeletionCompleted"), cancellationToken);
            return requested ? new(mediaId) : new(Forbidden: true);
        }
        if (media.LanguageId is not { } language || !actor.CanEdit(language, media.UploadedBy))
            return new(Forbidden: true);
        if (media.StorageState != "Deleting") return new(Error: "Request recording deletion first.");
        if (!options.Value.Enabled) return new(Unavailable: true);
        try
        {
            if (!await cleanup.RemoveLockedAsync(media, actorId, traceId, cancellationToken))
                return new(Error: "This recording is still used by learning material.");
        }
        catch (Exception error) when (error is AmazonS3Exception or HttpRequestException or TimeoutException
            || error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new(Unavailable: true);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(mediaId);
    }

    public async Task<MediaOutcome> ReviewAsync(Guid actorId, Guid familyId, Guid mediaId, string decision, string comment,
        string traceId, CancellationToken cancellationToken)
    {
        if (decision is not ("approve" or "reject") || string.IsNullOrWhiteSpace(comment) || comment.Length > 2000 || comment.Contains('\0'))
            return new(Error: "Choose approve or reject and add a review comment.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        var media = await LockAsync(mediaId, cancellationToken);
        if (actor is null || media is null || media.LanguageId is not { } language
            || !actor.CanReview(language, media.UploadedBy, media.UploadedBy)) return new(Forbidden: true);
        if (media.StorageState != "Ready" || media.IsVerified) return new(Error: "Only an available, unverified upload can be reviewed.");
        if (media.Sha256 is null || media.DurationMilliseconds is null) return new(Error: "Re-upload this legacy file through audio validation before review.");
        media.IsVerified = decision == "approve";
        media.VerifiedBy = media.IsVerified ? actorId : null;
        media.VerifiedAt = media.IsVerified ? clock.GetUtcNow() : null;
        Audit(actorId, mediaId, media.IsVerified ? "MediaApproved" : "MediaRejected", comment.Trim(), traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(mediaId);
    }

    public async Task<MediaOutcome> DeleteAsync(Guid actorId, Guid familyId, Guid mediaId, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        var media = await LockAsync(mediaId, cancellationToken);
        if (actor is null || media is null || media.LanguageId is not { } language || !actor.CanEdit(language, media.UploadedBy))
            return new(Forbidden: true);
        if (await database.Exercises.AnyAsync(item => item.AudioMediaId == mediaId, cancellationToken) || await database.SessionExercises.AnyAsync(item => item.AudioMediaId == mediaId, cancellationToken)
            || await database.ExerciseOptions.AnyAsync(item => item.AudioMediaId == mediaId, cancellationToken)
            || await database.SessionOptionAudio.AnyAsync(item => item.AudioMediaId == mediaId, cancellationToken)
            || await database.DialogueNodes.AnyAsync(item => item.AudioMediaId == mediaId, cancellationToken)
            || await database.VocabularyItems.AnyAsync(item => item.AudioMediaId == mediaId, cancellationToken))
            return new(Error: "This audio belongs to saved learning material and cannot be deleted.");
        if (media.StorageState == "Deleting") return new(mediaId);
        media.StorageState = "Deleting";
        media.IsVerified = false;
        media.VerifiedBy = null;
        media.VerifiedAt = null;
        Audit(actorId, mediaId, "MediaDeletionRequested", "Unused audio scheduled for durable private-object deletion.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(mediaId);
    }

    private Task<MediaAsset?> LockAsync(Guid id, CancellationToken cancellationToken) => database.MediaAssets
        .FromSqlInterpolated($"SELECT * FROM \"MediaAssets\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);

    private void Audit(Guid actorId, Guid id, string action, string reason, string traceId) => database.AuditLogs.Add(new AuditLog
    {
        ActorId = actorId,
        EntityType = "MediaAsset",
        EntityId = id.ToString(),
        Action = action,
        Reason = reason,
        TraceId = traceId,
        OccurredAt = clock.GetUtcNow()
    });
}
