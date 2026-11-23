using System.Data;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class WeeklySummaryQueries(LexiGoDbContext database, TimeProvider clock)
{
    public async Task<WeeklySummaryView?> GetAsync(Guid userId, int weeksAgo, CancellationToken cancellationToken)
    {
        if (weeksAgo is < 0 or > 12) return null;
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var week = await ReadAsync(userId, weeksAgo, clock.GetUtcNow(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return week;
    }

    public async Task<WeeklyComparison?> CompareAsync(Guid userId, int weeksAgo, CancellationToken cancellationToken)
    {
        if (weeksAgo is < 0 or > 11) return null;
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var now = clock.GetUtcNow();
        var selected = await ReadAsync(userId, weeksAgo, now, cancellationToken);
        if (selected is null) return null;
        var previous = await ReadAsync(userId, weeksAgo + 1, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return previous is null ? null : new(WeeklyComparisonWeek.From(selected), WeeklyComparisonWeek.From(previous));
    }

    private async Task<WeeklySummaryView?> ReadAsync(Guid userId, int weeksAgo, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var preference = await database.UserPreferences.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference is null) return null;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(preference.TimeZone)).DateTime);
        int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var start = today.AddDays(-daysSinceMonday - weeksAgo * 7);
        string zone = TimeZoneInfo.TryConvertWindowsIdToIanaId(preference.TimeZone, out var iana) ? iana : preference.TimeZone;
        var bounds = await WeeklyStudyDays.BoundsAsync(database, start, zone, cancellationToken);
        var answers = from answer in database.AnswerAttempts
                      join exercise in database.SessionExercises on answer.SessionExerciseId equals exercise.Id
                      join session in database.LearningSessions on exercise.SessionId equals session.Id
                      where answer.UserId == userId && exercise.UserId == userId && session.UserId == userId
                          && session.PlacementMap == null && answer.IsGraded && answer.SubmittedAt >= bounds.Start
                          && answer.SubmittedAt < bounds.End && answer.SubmittedAt <= now
                      select new { session.CourseId, answer.IsCorrect };
        var completions = from progress in database.LessonProgress
                          join lesson in database.Lessons on progress.LessonId equals lesson.Id
                          join unit in database.Units on lesson.UnitId equals unit.Id
                          where progress.UserId == userId && progress.Status == "Completed" && progress.CompletedAt >= bounds.Start
                              && progress.CompletedAt < bounds.End && progress.CompletedAt <= now
                          select unit.CourseId;
        var reviews = from review in database.WordReviews
                      join saved in database.SavedWords on review.SavedWordId equals saved.Id
                      join word in database.VocabularyItems on saved.VocabularyItemId equals word.Id
                      where saved.UserId == userId && review.ReviewedAt >= bounds.Start && review.ReviewedAt < bounds.End && review.ReviewedAt <= now
                      select new { word.CourseId, word.Id };
        var due = from saved in database.SavedWords
                  join word in database.VocabularyItems on saved.VocabularyItemId equals word.Id
                  where saved.UserId == userId && saved.DueAt <= now
                  select word.CourseId;
        var conversations = from session in database.DialogueSessions
                            join dialogue in database.Dialogues on session.DialogueId equals dialogue.Id
                            where session.UserId == userId && session.CompletedAt >= bounds.Start
                                && session.CompletedAt < bounds.End && session.CompletedAt <= now
                            select new { dialogue.CourseId, dialogue.Scenario };
        var courses = await (from enrollment in database.Enrollments.AsNoTracking()
                             join course in database.Courses on enrollment.CourseId equals course.Id
                             where enrollment.UserId == userId
                             orderby course.OrderNumber, course.Id
                             select new WeeklyCourseSummary(course.Id, course.Title, course.TargetLanguage.Code, course.IsPublished,
                                 database.XpTransactions.Where(item => item.UserId == userId && item.CourseId == course.Id
                                     && item.EarnedAt >= bounds.Start && item.EarnedAt < bounds.End && item.EarnedAt <= now).Sum(item => (long)item.Amount),
                                 completions.Count(id => id == course.Id),
                                 reviews.Where(item => item.CourseId == course.Id).Select(item => item.Id).Distinct().Count(),
                                 answers.Count(item => item.CourseId == course.Id),
                                 answers.Count(item => item.CourseId == course.Id && item.IsCorrect),
                                 due.Count(id => id == course.Id),
                                 conversations.Where(item => item.CourseId == course.Id)
                                     .Select(item => item.Scenario).Distinct().Count())).Take(20).ToArrayAsync(cancellationToken);
        var recorded = (await WeeklyStudyDays.ReadAsync(database, userId, bounds, now, zone, cancellationToken))
            .ToDictionary(item => item.StudyDate);
        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = start.AddDays(offset);
            return new WeeklySummaryDay(date, recorded.GetValueOrDefault(date)?.EarnedXp ?? 0, recorded.ContainsKey(date));
        }).ToArray();
        return new(start, start.AddDays(6), preference.TimeZone, today, weeksAgo, days, courses);
    }
}

public sealed record WeeklyComparison(WeeklyComparisonWeek Selected, WeeklyComparisonWeek Previous);

public sealed record WeeklyComparisonWeek(DateOnly WeekStart, int WeeksAgo, long EarnedXp,
    int NewLessonsCompleted, int StudyDays, int? Accuracy)
{
    public static WeeklyComparisonWeek From(WeeklySummaryView week) =>
        new(week.WeekStart, week.WeeksAgo, week.EarnedXp, week.NewLessonsCompleted, week.StudyDays, week.Accuracy);
}
