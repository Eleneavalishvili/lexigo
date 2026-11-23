using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class ChallengeProgression(LexiGoDbContext database, WeeklyChallengeCatalog catalog)
{
    // The completion caller holds the account lock and includes lesson XP in this transaction.
    internal async Task CompleteLessonLockedAsync(Guid userId, Guid courseId, DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Challenge progression requires an account-locked transaction.");

        // Use the persisted completion timestamp even if processing crosses a UTC week boundary.
        await catalog.EnsureWeekAsync(completedAt, cancellationToken);
        var now = completedAt;
        var challenges = await database.Challenges
            .FromSqlRaw("SELECT * FROM \"Challenges\" ORDER BY \"Id\" FOR SHARE").AsNoTracking()
            .Where(item => !item.IsArchived && item.StartsAt <= now && item.EndsAt > now)
            .OrderBy(item => item.Id).ToArrayAsync(cancellationToken);
        var challengeIds = challenges.Select(item => item.Id).ToArray();
        var states = await database.UserChallenges.Where(item => item.UserId == userId && challengeIds.Contains(item.ChallengeId))
            .ToDictionaryAsync(item => item.ChallengeId, cancellationToken);
        foreach (var challenge in challenges)
        {
            states.TryGetValue(challenge.Id, out var state);
            if (state?.RewardedAt is not null) continue;
            var source = database.XpTransactions.Where(item => item.UserId == userId
                && item.SourceType == "FirstLessonCompletion"
                && item.EarnedAt >= challenge.StartsAt && item.EarnedAt < challenge.EndsAt && item.EarnedAt <= now);
            int progress = challenge.RuleType switch
            {
                "CompletedLessons" => await source.CountAsync(cancellationToken),
                "StudyDays" => await source.Select(item => item.EarnedAt.UtcDateTime.Date).Distinct().CountAsync(cancellationToken),
                _ => throw new InvalidOperationException("Unknown challenge rule.")
            };
            if (state is null)
            {
                state = new UserChallenge { UserId = userId, ChallengeId = challenge.Id };
                database.UserChallenges.Add(state);
            }
            state.Progress = Math.Min(challenge.Target, Math.Max(state.Progress, progress));
            if (state.Progress < challenge.Target) continue;

            state.CompletedAt ??= now;
            state.RewardedAt = now;
            database.XpTransactions.Add(new XpTransaction
            {
                UserId = userId,
                CourseId = courseId,
                SourceType = "Challenge",
                SourceId = challenge.Id,
                Amount = challenge.RewardXp,
                EarnedAt = now
            });
            await AddDailyXpAsync(userId, challenge.RewardXp, now, cancellationToken);
            database.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = "Challenge",
                Title = "Challenge complete: " + challenge.Title,
                Body = "You earned " + challenge.RewardXp + " bonus XP in the language of your completing lesson.",
                CreatedAt = now
            });
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task AddDailyXpAsync(Guid userId, int amount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var preferences = await database.UserPreferences.SingleAsync(item => item.UserId == userId, cancellationToken);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
            TimeZoneInfo.FindSystemTimeZoneById(preferences.TimeZone)).DateTime);
        var activity = await database.DailyActivities.SingleOrDefaultAsync(
            item => item.UserId == userId && item.LocalDate == date, cancellationToken);
        if (activity is null)
        {
            activity = new DailyActivity { UserId = userId, LocalDate = date };
            database.DailyActivities.Add(activity);
        }
        activity.EarnedXp += amount;
        activity.GoalReached |= activity.EarnedXp >= preferences.DailyGoalXp;
    }
}
