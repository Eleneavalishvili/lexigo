using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed record ManagedAchievement(Guid Id, string Code, string Title, string RuleType, int Threshold,
    bool IsArchived, int Revision, bool CanEdit);
public sealed record ManagedChallenge(Guid Id, string Title, string RuleType, int Target, int RewardXp,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsArchived, int Revision, bool CanEdit);

public sealed class RewardCatalogQueries(LexiGoDbContext database, TimeProvider clock)
{
    private IQueryable<ManagedAchievement> Achievements(IQueryable<Achievement> source) =>
        source.Select(item => new ManagedAchievement(item.Id, item.Code,
            item.Title, item.RuleType, item.Threshold, item.IsArchived, item.Revision,
            !item.IsArchived && !database.UserAchievements.Any(award => award.AchievementId == item.Id)));

    private IQueryable<ManagedChallenge> Challenges(IQueryable<Challenge> source)
    {
        var now = clock.GetUtcNow();
        return source.Select(item => new ManagedChallenge(item.Id, item.Title,
            item.RuleType, item.Target, item.RewardXp, item.StartsAt, item.EndsAt, item.IsArchived, item.Revision,
            !item.IsArchived && item.StartsAt > now && !database.UserChallenges.Any(state => state.ChallengeId == item.Id)));
    }

    public async Task<CatalogPage<ManagedAchievement>> AchievementsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var source = database.Achievements.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
            source = source.Where(item => item.Code.ToLower().Contains(term) || item.Title.ToLower().Contains(term));
        }
        int total = await source.CountAsync(cancellationToken);
        var items = await Achievements(source.OrderBy(item => item.Code).Skip((page - 1) * pageSize).Take(pageSize))
            .ToArrayAsync(cancellationToken);
        return new(items, page, pageSize, (long)page * pageSize < total, total);
    }

    public async Task<CatalogPage<ManagedChallenge>> ChallengesAsync(int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var source = database.Challenges.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
            source = source.Where(item => item.Title.ToLower().Contains(term));
        }
        int total = await source.CountAsync(cancellationToken);
        var items = await Challenges(source.OrderByDescending(item => item.StartsAt).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToArrayAsync(cancellationToken);
        return new(items, page, pageSize, (long)page * pageSize < total, total);
    }

    public Task<ManagedAchievement?> AchievementAsync(Guid id, CancellationToken cancellationToken) =>
        Achievements(database.Achievements.AsNoTracking().Where(item => item.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public Task<ManagedChallenge?> ChallengeAsync(Guid id, CancellationToken cancellationToken) =>
        Challenges(database.Challenges.AsNoTracking().Where(item => item.Id == id)).SingleOrDefaultAsync(cancellationToken);
}
