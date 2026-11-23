using System.Security.Cryptography;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Accounts;

public enum AvatarWriteResult { Saved, Missing, LimitReached, Unavailable }

public sealed class ProfileAvatarService(LexiGoDbContext database, LearningAccess access,
    Lazy<MediaObjectStore> store, IOptions<ObjectStorageOptions> options, TimeProvider clock,
    ILogger<ProfileAvatarService> logger)
{
    internal const string MediaSource = "LexiGo profile avatar";

    private IQueryable<MediaAsset> OwnedAvatars(Guid userId) => database.MediaAssets.Where(item =>
        item.UploadedBy == userId && item.MimeType == "image/png" && item.Source == MediaSource);

    public async Task<string?> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return null;
        var key = await (from user in database.Users.AsNoTracking()
                         join media in OwnedAvatars(userId) on user.AvatarMediaId equals (Guid?)media.Id
                         where user.Id == userId && user.Status == "Active" && media.StorageState == "Ready"
                         select media.StorageKey).SingleOrDefaultAsync(cancellationToken);
        return key is null ? null : await store.Value.CreateReadLinkAsync(key, TimeSpan.FromMinutes(2));
    }

    public async Task<AvatarWriteResult> SaveAsync(Guid userId, Guid familyId, byte[] png, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return AvatarWriteResult.Unavailable;
        var avatar = new MediaAsset
        {
            UploadedBy = userId, StorageKey = "", MimeType = "image/png", ByteSize = png.Length,
            Source = MediaSource, License = "Private profile use", StorageState = "Pending", CreatedAt = clock.GetUtcNow(),
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(png))
        };
        avatar.StorageKey = $"media/{avatar.Id:N}";
        await using (var reservation = await database.Database.BeginTransactionAsync(cancellationToken))
        {
            if (!await access.LockAsync(userId, familyId, cancellationToken)) return AvatarWriteResult.Missing;
            if (await OwnedAvatars(userId).CountAsync(cancellationToken) >= 5) return AvatarWriteResult.LimitReached;
            database.MediaAssets.Add(avatar);
            await database.SaveChangesAsync(cancellationToken);
            await reservation.CommitAsync(cancellationToken);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return AvatarWriteResult.Missing;
        var reserved = await database.MediaAssets.FromSqlInterpolated(
            $"SELECT * FROM \"MediaAssets\" WHERE \"Id\" = {avatar.Id} FOR UPDATE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (reserved is not { StorageState: "Pending" }) return AvatarWriteResult.Unavailable;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await store.Value.UploadAvatarAsync(avatar.Id, png, timeout.Token);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Profile avatar upload {MediaId} was interrupted and is queued for cleanup.", avatar.Id);
            return AvatarWriteResult.Unavailable;
        }
        var user = await database.Users.SingleAsync(item => item.Id == userId, cancellationToken);
        await RetireAsync(userId, user.AvatarMediaId, cancellationToken);
        avatar.StorageState = "Ready";
        user.AvatarMediaId = avatar.Id;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AvatarWriteResult.Saved;
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid familyId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return false;
        var user = await database.Users.SingleAsync(item => item.Id == userId, cancellationToken);
        await RetireAsync(userId, user.AvatarMediaId, cancellationToken);
        user.AvatarMediaId = null;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task RetireAsync(Guid userId, Guid? avatarId, CancellationToken cancellationToken)
    {
        if (avatarId is null) return;
        await database.UserPreferences.Where(item => item.UserId == userId && item.SharedLeaderboardAvatarId == avatarId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.SharedLeaderboardAvatarId, (Guid?)null), cancellationToken);
        await OwnedAvatars(userId).Where(item => item.Id == avatarId && item.StorageState == "Ready")
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.StorageState, "Deleting"), cancellationToken);
    }
}
