using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public static class ReviewedAudio
{
    public static Task<MediaAsset[]> LockAsync(LexiGoDbContext database, Guid[] ids, CancellationToken cancellationToken) => ids.Length == 0
        ? Task.FromResult(Array.Empty<MediaAsset>())
        : database.MediaAssets.FromSqlInterpolated($"SELECT * FROM \"MediaAssets\" WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR UPDATE")
            .ToArrayAsync(cancellationToken);

    public static IQueryable<MediaAsset> ForLanguage(IQueryable<MediaAsset> media, int languageId) =>
        Ready(media).Where(item => item.LanguageId == languageId);

    public static IQueryable<MediaAsset> Ready(IQueryable<MediaAsset> media) => media.Where(item =>
        item.StorageState == "Ready" && item.IsVerified
        && item.VerifiedBy != null && item.VerifiedAt != null && item.Sha256 != null && item.DurationMilliseconds != null
        && item.Source != "" && item.License != "");
}
