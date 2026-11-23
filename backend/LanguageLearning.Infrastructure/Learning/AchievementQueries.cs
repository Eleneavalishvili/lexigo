using System.Data;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed record AchievementView(Guid Id, string Code, string Title, string RuleType,
    int Threshold, int Progress, DateTimeOffset? EarnedAt)
{
    public bool CanCollect => EarnedAt is null && Progress >= Threshold;
}

public sealed class AchievementQueries(LexiGoDbContext database, AchievementProgressQueries progress)
{
    public async Task<AchievementView[]> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var values = await progress.GetAsync(userId, cancellationToken);
        var catalog = await database.Achievements.AsNoTracking()
            .Where(item => !item.IsArchived || database.UserAchievements.Any(award => award.UserId == userId && award.AchievementId == item.Id))
            .OrderBy(item => item.Id).ToArrayAsync(cancellationToken);
        var awards = await database.UserAchievements.AsNoTracking().Where(item => item.UserId == userId)
            .ToDictionaryAsync(item => item.AchievementId, item => item.EarnedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return catalog.Select(item => new AchievementView(item.Id, item.Code, item.Title, item.RuleType,
            item.Threshold, values.For(item.RuleType), awards.TryGetValue(item.Id, out var date) ? date : null)).ToArray();
    }
}
