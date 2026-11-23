using System.Data;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class LeaderboardQueries(LexiGoDbContext database, TimeProvider clock,
    Lazy<MediaObjectStore> storage, IOptions<ObjectStorageOptions> storageOptions,
    ILogger<LeaderboardQueries> logger, RankingReportTokens reportTokens)
{
    public DateOnly CurrentWeek()
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
    }

    public async Task<LeaderboardView?> GetAsync(Guid userId, Guid courseId, DateOnly week,
        int page, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var course = await (from enrollment in database.Enrollments.AsNoTracking()
                            join value in database.Courses on enrollment.CourseId equals value.Id
                            where enrollment.UserId == userId && value.Id == courseId
                            select new { value.Title }).SingleOrDefaultAsync(cancellationToken);
        if (course is null) return null;
        DateTimeOffset start = new(week.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        DateTimeOffset end = start.AddDays(7);
        var ranking = LeaderboardScores.For(database, courseId, week);
        int total = await ranking.CountAsync(cancellationToken);
        var rows = await ranking.OrderBy(item => item.Rank).Skip((page - 1) * 20).Take(20).ToArrayAsync(cancellationToken);
        var ownRank = await ranking.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        long ownXp = await database.XpTransactions.Where(item => item.UserId == userId && item.CourseId == courseId
            && item.EarnedAt >= start && item.EarnedAt < end).SumAsync(item => (long)item.Amount, cancellationToken);
        var preference = await database.UserPreferences.Where(item => item.UserId == userId)
            .Select(item => new { item.LeaderboardOptIn, item.SharedLeaderboardAvatarId })
            .SingleAsync(cancellationToken);
        Dictionary<Guid, string> photoKeys = [];
        if (storageOptions.Value.Enabled && rows.Length > 0)
        {
            var participantIds = rows.Where(item => item.UserId != userId).Select(item => item.UserId).ToArray();
            photoKeys = await (from user in database.Users.AsNoTracking()
                               join prefs in database.UserPreferences on user.Id equals prefs.UserId
                               join media in database.MediaAssets on prefs.SharedLeaderboardAvatarId equals (Guid?)media.Id
                               where participantIds.Contains(user.Id) && prefs.LeaderboardOptIn
                                   && user.AvatarMediaId == media.Id && media.UploadedBy == user.Id
                                   && media.StorageState == "Ready" && media.MimeType == "image/png"
                                   && media.Source == ProfileAvatarService.MediaSource
                               select new { user.Id, media.StorageKey }).ToDictionaryAsync(
                item => item.Id, item => item.StorageKey, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        LeaderboardEntry Public(LeaderboardRow item, string? photoUrl = null) =>
            new(item.Rank, item.DisplayName, item.Xp, item.UserId == userId, photoUrl,
                item.UserId == userId ? null : reportTokens.Issue(item.UserId, courseId, week, item.DisplayName));
        var entries = await Task.WhenAll(rows.Select(async item => Public(item,
            photoKeys.TryGetValue(item.UserId, out var key)
                ? await TrySignPhotoAsync(key)
                : null)));
        return new(courseId, course.Title, week, CurrentWeek(), preference.LeaderboardOptIn, ownXp,
            ownRank is null ? null : Public(ownRank), entries, total, page, 20,
            preference.SharedLeaderboardAvatarId is not null);
    }

    private async Task<string?> TrySignPhotoAsync(string key)
    {
        try
        {
            return await storage.Value.CreateReadLinkAsync(key, TimeSpan.FromSeconds(30));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Amazon.S3.AmazonS3Exception)
        {
            logger.LogWarning("A leaderboard avatar was unavailable. Showing initials instead.");
            return null;
        }
    }
}

