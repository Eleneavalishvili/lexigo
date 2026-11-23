using System.Security.Cryptography;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class CompletionCardIssuer(LexiGoDbContext database, ContentTransactions content, TimeProvider clock)
{
    // Caller holds the learner row lock and owns the transaction.
    public async Task<(Guid Id, bool Created)?> IssueForUnitLockedAsync(Guid userId, Guid unitId, CancellationToken cancellationToken)
    {
        var existing = await database.CompletionCards.SingleOrDefaultAsync(item => item.UserId == userId && item.UnitId == unitId, cancellationToken);
        if (existing is not null) return (existing.Id, false);
        var unit = await database.Units.AsNoTracking().SingleOrDefaultAsync(item => item.Id == unitId, cancellationToken);
        if (unit is null || !await database.Enrollments.AnyAsync(item => item.UserId == userId && item.CourseId == unit.CourseId, cancellationToken)) return null;
        var course = await content.LockCourseAsync(unit.CourseId, cancellationToken);
        if (course is null) return null;
        unit = await database.Units.AsNoTracking().SingleOrDefaultAsync(item => item.Id == unitId && item.CourseId == course.Id, cancellationToken);
        if (unit is null) return null;
        var lessonIds = await (from lesson in database.Lessons
                               join version in database.LessonVersions on lesson.CurrentVersionId equals version.Id
                               where lesson.UnitId == unitId && lesson.Status == "Published"
                                   && version.Status == "Published" && version.LessonId == lesson.Id
                               select lesson.Id).ToArrayAsync(cancellationToken);
        if (lessonIds.Length == 0) return null;
        var progress = await database.LessonProgress.AsNoTracking().Where(item => item.UserId == userId
            && lessonIds.Contains(item.LessonId) && item.Status == "Completed" && item.CompletedAt != null)
            .ToArrayAsync(cancellationToken);
        if (progress.Length != lessonIds.Length) return null;
        var now = clock.GetUtcNow();
        if (progress.Any(item => item.CompletedAt > now)) return null;
        var gradedLessons = await (from session in database.LearningSessions
                                   join exercise in database.SessionExercises on session.Id equals exercise.SessionId
                                   join answer in database.AnswerAttempts on exercise.Id equals answer.SessionExerciseId
                                   where session.UserId == userId && exercise.UserId == userId && answer.UserId == userId
                                       && session.Mode == "Lesson" && session.CompletedAt != null && answer.IsGraded
                                       && session.LessonId != null && lessonIds.Contains(session.LessonId.Value)
                                   select session.LessonId!.Value).Distinct().ToArrayAsync(cancellationToken);
        var graded = progress.Where(item => gradedLessons.Contains(item.LessonId)).ToArray();
        var card = new CompletionCard
        {
            UserId = userId,
            CourseId = course.Id,
            UnitId = unitId,
            CourseTitle = course.Title,
            UnitTitle = unit.Title,
            IssuedAt = now,
            CompletedAt = progress.Max(item => item.CompletedAt!.Value),
            VerificationCode = NewCode(),
            LessonCount = lessonIds.Length,
            GradedLessonCount = graded.Length,
            AverageBestScore = graded.Length == 0 ? null : Math.Round(graded.Average(item => (decimal)item.BestScore), 2)
        };
        database.CompletionCards.Add(card);
        await database.SaveChangesAsync(cancellationToken);
        return (card.Id, true);
    }

    public async Task IssueForLessonLockedAsync(Guid userId, Guid lessonId, CancellationToken cancellationToken)
    {
        var unitId = await database.Lessons.Where(item => item.Id == lessonId).Select(item => (Guid?)item.UnitId).SingleOrDefaultAsync(cancellationToken);
        if (unitId is { } unit) await IssueForUnitLockedAsync(userId, unit, cancellationToken);
    }

    internal static string NewCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
