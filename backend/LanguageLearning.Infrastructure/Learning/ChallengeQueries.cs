using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed record ChallengeView(Guid Id, string Title, string RuleType, int Target, int RewardXp,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Progress, DateTimeOffset? CompletedAt,
    DateTimeOffset? RewardedAt, Guid? RewardCourseId, bool IsActive);

public sealed class ChallengeQueries(LexiGoDbContext database, TimeProvider clock)
{
    public async Task<ChallengeView[]> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await (from challenge in database.Challenges.AsNoTracking()
                      join progress in database.UserChallenges.Where(item => item.UserId == userId)
                          on challenge.Id equals progress.ChallengeId into owned
                      from state in owned.DefaultIfEmpty()
                      where challenge.StartsAt <= now && ((!challenge.IsArchived && challenge.EndsAt > now) || state != null)
                      orderby challenge.StartsAt descending, challenge.Id
                      select new ChallengeView(challenge.Id, challenge.Title, challenge.RuleType,
                          challenge.Target, challenge.RewardXp, challenge.StartsAt, challenge.EndsAt,
                          state != null && (state.RewardedAt != null || challenge.EndsAt <= now)
                              ? state.Progress
                              : Math.Min(challenge.Target, challenge.RuleType == "CompletedLessons"
                                  ? database.XpTransactions.Count(xp => xp.UserId == userId
                                      && xp.SourceType == "FirstLessonCompletion" && xp.EarnedAt >= challenge.StartsAt
                                      && xp.EarnedAt < challenge.EndsAt && xp.EarnedAt <= now)
                                  : database.XpTransactions.Where(xp => xp.UserId == userId
                                      && xp.SourceType == "FirstLessonCompletion" && xp.EarnedAt >= challenge.StartsAt
                                      && xp.EarnedAt < challenge.EndsAt && xp.EarnedAt <= now)
                                      .Select(xp => xp.EarnedAt.UtcDateTime.Date).Distinct().Count()),
                          state == null ? null : state.CompletedAt,
                          state == null ? null : state.RewardedAt,
                          database.XpTransactions.Where(xp => xp.UserId == userId && xp.SourceType == "Challenge"
                              && xp.SourceId == challenge.Id).Select(xp => (Guid?)xp.CourseId).FirstOrDefault(),
                          challenge.EndsAt > now))
            .Take(50).ToArrayAsync(cancellationToken);
    }
}
