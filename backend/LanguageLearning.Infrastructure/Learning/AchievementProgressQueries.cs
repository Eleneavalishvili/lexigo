using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed record AchievementProgress(int Lessons, int GoalStreak, int Words, bool PerfectLesson)
{
    public int For(string ruleType) => ruleType switch
    {
        "CompletedLessons" => Lessons,
        "GoalStreak" => GoalStreak,
        "SavedWords" => Words,
        "PerfectLesson" => PerfectLesson ? 1 : 0,
        _ => throw new InvalidOperationException("Unknown achievement rule.")
    };
}

public sealed class AchievementProgressQueries(LexiGoDbContext database, TimeProvider clock)
{
    public async Task<AchievementProgress> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        int lessons = await database.LessonProgress.CountAsync(item => item.UserId == userId && item.Status == "Completed", cancellationToken);
        int words = await database.SavedWords.CountAsync(item => item.UserId == userId, cancellationToken);
        bool perfect = await database.LearningSessions.AnyAsync(session => session.UserId == userId && session.Mode == "Lesson"
            && session.CompletedAt != null && session.Score == 100
            && (from exercise in database.SessionExercises
                join attempt in database.AnswerAttempts on exercise.Id equals attempt.SessionExerciseId
                where exercise.SessionId == session.Id && attempt.UserId == userId && attempt.IsGraded
                select attempt.Id).Any()
            && !database.SessionExercises.Any(exercise => exercise.SessionId == session.Id
                && !database.AnswerAttempts.Any(attempt => attempt.SessionExerciseId == exercise.Id && attempt.UserId == userId)), cancellationToken);
        string zone = await database.UserPreferences.Where(item => item.UserId == userId).Select(item => item.TimeZone).SingleAsync(cancellationToken);
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
        int streak = await database.Database.SqlQuery<int>($"""
            WITH days AS (
                SELECT "LocalDate" - row_number() OVER (ORDER BY "LocalDate")::integer AS run
                FROM "DailyActivities"
                WHERE "UserId" = {userId} AND "GoalReached" AND "LocalDate" <= {today}
            ), runs AS (SELECT count(*) AS length FROM days GROUP BY run)
            SELECT coalesce(max(length), 0)::integer AS "Value" FROM runs
            """).SingleAsync(cancellationToken);
        return new(lessons, streak, words, perfect);
    }
}
