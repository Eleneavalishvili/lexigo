using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Content;

public sealed class MediaQueries(LexiGoDbContext database, ContentAccess access, Lazy<MediaObjectStore> store, IOptions<ObjectStorageOptions> options)
{
    public async Task<StaffMedia[]?> ListAsync(Guid actorId, Guid familyId, int page, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        if (actor is null) return null;
        return await Views(Visible(actor).OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .Skip((page - 1) * 25).Take(25)).ToArrayAsync(cancellationToken);
    }

    public async Task<StaffMedia?> GetAsync(Guid actorId, Guid familyId, Guid id, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        return actor is null ? null : await Views(Visible(actor).Where(item => item.Id == id)).SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<StaffMedia> Views(IQueryable<MediaAsset> source) =>
        source.Select(item => new StaffMedia(item.Id, item.LanguageId, item.MimeType, item.ByteSize, item.DurationMilliseconds,
                item.Source, item.License, item.UploadedBy, item.IsVerified, item.StorageState, item.CreatedAt, item.VerifiedBy,
                database.AuditLogs.Where(review => review.EntityType == "MediaAsset" && review.EntityId == item.Id.ToString()
                    && (review.Action == "MediaApproved" || review.Action == "MediaRejected"))
                    .OrderByDescending(review => review.OccurredAt).ThenByDescending(review => review.Id).Select(review => review.Reason).FirstOrDefault()));

    public async Task<string?> PreviewAsync(Guid actorId, Guid familyId, Guid mediaId, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        if (actor is null || !options.Value.Enabled) return null;
        var media = await Visible(actor).SingleOrDefaultAsync(item => item.Id == mediaId && item.StorageState == "Ready", cancellationToken);
        return media is null ? null : await store.Value.CreateReadLinkAsync(media.StorageKey, TimeSpan.FromMinutes(2));
    }

    private IQueryable<MediaAsset> Visible(ContentActor actor)
    {
        bool reviewer = actor.Roles.Contains(RoleNames.Reviewer);
        return database.MediaAssets.AsNoTracking().Where(item => actor.IsAdmin || item.UploadedBy == actor.Id
            || reviewer && item.LanguageId.HasValue && actor.LanguageIds.Contains(item.LanguageId.Value));
    }
}
