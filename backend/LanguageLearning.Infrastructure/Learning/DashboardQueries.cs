using System.Data;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class DashboardQueries(LexiGoDbContext database, HeartBalanceService hearts, TimeProvider clock)
{
    public async Task<DashboardView?> GetAsync(Guid userId, Guid? courseId, CancellationToken cancellationToken)
    {
        // All counters come from one database snapshot, even if a lesson finishes during this read.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var preference = await database.UserPreferences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference is null) return null;

        Guid? selectedCourse = courseId ?? preference.ActiveCourseId;
        DashboardCourse? course = null;
        if (selectedCourse is { } id)
        {
            var enrolled = await (from enrollment in database.Enrollments.AsNoTracking()
                                  join value in database.Courses on enrollment.CourseId equals value.Id
                                  where enrollment.UserId == userId && value.Id == id
                                  select new { value.Id, value.Title, LanguageCode = value.TargetLanguage.Code })
                .SingleOrDefaultAsync(cancellationToken);
            if (enrolled is null && courseId is not null) return null;
            if (enrolled is not null)
            {
                long courseXp = await database.XpTransactions.Where(item => item.UserId == userId && item.CourseId == id)
                    .SumAsync(item => (long)item.Amount, cancellationToken);
                int courseLessons = await (from progress in database.LessonProgress.AsNoTracking()
                                           join lesson in database.Lessons on progress.LessonId equals lesson.Id
                                           join unit in database.Units on lesson.UnitId equals unit.Id
                                           where progress.UserId == userId && progress.Status == "Completed" && unit.CourseId == id
                                           select progress.LessonId).CountAsync(cancellationToken);
                course = new(enrolled.Id, enrolled.Title, enrolled.LanguageCode, courseXp, courseLessons);
            }
        }

        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(preference.TimeZone)).DateTime);
        var activity = await database.DailyActivities.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.LocalDate == today, cancellationToken);
        long totalXp = await database.XpTransactions.Where(item => item.UserId == userId)
            .SumAsync(item => (long)item.Amount, cancellationToken);
        int completed = await database.LessonProgress.CountAsync(item => item.UserId == userId && item.Status == "Completed", cancellationToken);
        int streak = await CurrentStreakAsync(userId, today, cancellationToken);
        var heartStatus = await hearts.StatusAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(totalXp, activity?.EarnedXp ?? 0, preference.DailyGoalXp,
            activity?.GoalReached ?? false, streak, completed, today, preference.TimeZone, heartStatus, course);
    }

    private async Task<int> CurrentStreakAsync(Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        DateOnly yesterday = today.AddDays(-1);
        // Count the contiguous suffix in SQL instead of loading a user's entire activity history.
        // Yesterday's streak survives until the end of today; a goal flag is historical evidence.
        return await database.Database.SqlQuery<int>($"""
            WITH days AS (
                SELECT "LocalDate", row_number() OVER (ORDER BY "LocalDate" DESC) AS position
                FROM "DailyActivities"
                WHERE "UserId" = {userId} AND "GoalReached" AND "LocalDate" <= {today}
            ), anchor AS (
                SELECT max("LocalDate") AS day FROM days WHERE "LocalDate" >= {yesterday}
            )
            SELECT count(*)::integer AS "Value" FROM days, anchor
            WHERE days."LocalDate" = anchor.day - (days.position::integer - 1)
            """).SingleAsync(cancellationToken);
    }
}
