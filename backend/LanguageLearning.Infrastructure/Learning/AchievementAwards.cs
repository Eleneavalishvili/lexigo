using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class AchievementAwards(LexiGoDbContext database, LearningAccess access,
    AchievementProgressQueries progress, TimeProvider clock)
{
    public async Task<AchievementView[]?> CollectAsync(Guid userId, Guid familyId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return null;
        var awarded = await AwardLockedAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return awarded;
    }

    // Completion and vocabulary callers already hold the account lock and enclosing transaction.
    internal async Task<AchievementView[]> AwardLockedAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Awards require an account-locked transaction.");
        var values = await progress.GetAsync(userId, cancellationToken);
        var existing = await database.UserAchievements.Where(item => item.UserId == userId)
            .Select(item => item.AchievementId).ToArrayAsync(cancellationToken);
        var available = await database.Achievements
            .FromSqlRaw("SELECT * FROM \"Achievements\" ORDER BY \"Id\" FOR SHARE")
            .AsNoTracking().Where(item => !item.IsArchived && !existing.Contains(item.Id)).ToArrayAsync(cancellationToken);
        var awarded = new List<AchievementView>();
        var now = clock.GetUtcNow();
        foreach (var item in available.Where(item => values.For(item.RuleType) >= item.Threshold))
        {
            database.UserAchievements.Add(new UserAchievement { UserId = userId, AchievementId = item.Id, EarnedAt = now });
            database.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = "Achievement",
                Title = "Achievement unlocked: " + item.Title,
                Body = "You earned “" + item.Title + "”. Visit Achievements to see your learning milestones.",
                CreatedAt = now
            });
            awarded.Add(new(item.Id, item.Code, item.Title, item.RuleType, item.Threshold, values.For(item.RuleType), now));
        }
        await database.SaveChangesAsync(cancellationToken);
        return awarded.ToArray();
    }
}
