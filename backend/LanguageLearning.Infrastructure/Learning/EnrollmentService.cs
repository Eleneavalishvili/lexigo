using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class EnrollmentService(LexiGoDbContext database, LearningAccess access, TimeProvider clock)
{
    public async Task<EnrollmentPage> ListAsync(Guid userId, int page, CancellationToken cancellationToken)
    {
        var query = from enrollment in database.Enrollments.AsNoTracking()
                    join course in database.Courses on enrollment.CourseId equals course.Id
                    where enrollment.UserId == userId
                    select new { CourseId = course.Id, course.Title, course.IsPublished, enrollment.EnrolledAt, enrollment.PlacementLevel };
        return new(await query.OrderByDescending(item => item.EnrolledAt).ThenBy(item => item.CourseId)
            .Skip((page - 1) * 20).Take(20).Select(item => new EnrollmentView(item.CourseId, item.Title, item.IsPublished, item.EnrolledAt, item.PlacementLevel))
            .ToArrayAsync(cancellationToken), page, 20, await query.CountAsync(cancellationToken));
    }

    public Task<EnrollmentView?> GetAsync(Guid userId, Guid courseId, CancellationToken cancellationToken) =>
        (from enrollment in database.Enrollments.AsNoTracking()
         join course in database.Courses on enrollment.CourseId equals course.Id
         where enrollment.UserId == userId && enrollment.CourseId == courseId
         select new EnrollmentView(course.Id, course.Title, course.IsPublished, enrollment.EnrolledAt,
             enrollment.PlacementLevel)).SingleOrDefaultAsync(cancellationToken);

    public async Task<LearningOutcome> EnrollAsync(Guid userId, Guid familyId, Guid courseId, CancellationToken cancellationToken, string? startingLevel = null)
    {
        if (startingLevel is not (null or "A1" or "A2" or "B1" or "B2" or "C1" or "C2")) return new(Error: "Choose a valid starting level.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var existing = await database.Enrollments.SingleOrDefaultAsync(item => item.UserId == userId && item.CourseId == courseId, cancellationToken);
        if (existing is not null)
        {
            if (startingLevel is not null) existing.PlacementLevel = "Self:" + startingLevel;
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(new EnrollmentReceipt(courseId, existing.EnrolledAt));
        }
        if (!await database.Courses.AnyAsync(item => item.Id == courseId && item.IsPublished, cancellationToken)) return new(Missing: true);
        var enrollment = new Enrollment { UserId = userId, CourseId = courseId, EnrolledAt = clock.GetUtcNow(), PlacementLevel = startingLevel is null ? null : "Self:" + startingLevel };
        database.Enrollments.Add(enrollment);
        await database.SaveChangesAsync(cancellationToken);
        await database.Entry(enrollment).ReloadAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(new EnrollmentReceipt(courseId, enrollment.EnrolledAt), Created: true);
    }

    public async Task<CourseProgress?> ProgressAsync(Guid userId, Guid courseId, CancellationToken cancellationToken)
    {
        var enrollment = await database.Enrollments.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.CourseId == courseId, cancellationToken);
        bool enrolled = enrollment is not null;
        var course = await database.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == courseId && (item.IsPublished || enrolled), cancellationToken);
        if (course is null) return null;
        var lessons = await (from lesson in database.Lessons.AsNoTracking()
                             join unit in database.Units on lesson.UnitId equals unit.Id
                             join version in database.LessonVersions on lesson.CurrentVersionId equals version.Id
                             where !course.ArchivedAt.HasValue && unit.ArchivedAt == null && unit.CourseId == courseId && lesson.Status == "Published"
                             orderby unit.OrderNumber, lesson.OrderNumber
                             select new { lesson.Id, lesson.UnitId, version.Title, version.Objective, lesson.OrderNumber }).Take(5001).ToArrayAsync(cancellationToken);
        if (lessons.Length > 5000) throw new InvalidOperationException("Course learning map exceeds the supported bound.");
        var ids = lessons.Select(item => item.Id).ToArray();
        var gradedLessonIds = await (from session in database.LearningSessions
                                     join exercise in database.SessionExercises on session.Id equals exercise.SessionId
                                     join attempt in database.AnswerAttempts on exercise.Id equals attempt.SessionExerciseId
                                     where session.UserId == userId && session.CourseId == courseId && session.Mode == "Lesson"
                                         && session.CompletedAt != null && session.LessonId != null && attempt.IsGraded && ids.Contains(session.LessonId!.Value)
                                     select session.LessonId!.Value).Distinct().ToArrayAsync(cancellationToken);
        var progress = await database.LessonProgress.AsNoTracking().Where(item => item.UserId == userId && ids.Contains(item.LessonId)).ToDictionaryAsync(item => item.LessonId, cancellationToken);
        // Retain completed prerequisites even when their catalog entry is subsequently archived.
        var completed = await (from item in database.LessonProgress
                               join lesson in database.Lessons on item.LessonId equals lesson.Id
                               join unit in database.Units on lesson.UnitId equals unit.Id
                               where item.UserId == userId && unit.CourseId == courseId && item.Status == "Completed"
                               select item.LessonId).ToArrayAsync(cancellationToken);
        var bypassed = await PlacementAccess.BypassedAsync(database, userId, courseId, cancellationToken);
        var recommendedUnitId = enrollment?.PlacementLevel?.StartsWith("Test:", StringComparison.Ordinal) == true
            ? await database.LearningSessions.AsNoTracking().Where(item => item.UserId == userId
            && item.CourseId == courseId && item.PlacementAppliedAt != null).OrderByDescending(item => item.PlacementAppliedAt)
            .Select(item => item.RecommendedUnitId).FirstOrDefaultAsync(cancellationToken) : null;
        var edges = await database.LessonPrerequisites.AsNoTracking().Where(item => ids.Contains(item.LessonId)).ToArrayAsync(cancellationToken);
        var active = await database.LearningSessions.AsNoTracking().Where(item => item.UserId == userId && item.CourseId == courseId && item.CompletedAt == null)
            .OrderByDescending(item => item.StartedAt).Select(item => new OpenLearningSession(item.Id, item.LessonId, item.Title, item.Mode)).ToArrayAsync(cancellationToken);
        var units = await database.Units.AsNoTracking().Where(item => item.CourseId == courseId && lessons.Select(row => row.UnitId).Distinct().ToArray().Contains(item.Id))
            .OrderBy(item => item.OrderNumber).Select(item => new LearningUnit(item.Id, item.Title, item.Description, item.OrderNumber)).ToArrayAsync(cancellationToken);
        return new(courseId, course.Title, enrolled, units, lessons.Select(item =>
        {
            progress.TryGetValue(item.Id, out var saved);
            Guid[] missing = edges.Where(edge => edge.LessonId == item.Id && !completed.Contains(edge.PrerequisiteLessonId)
                && !bypassed.Contains(edge.PrerequisiteLessonId)).Select(edge => edge.PrerequisiteLessonId).ToArray();
            return new LessonMapItem(item.Id, item.UnitId, item.Title, item.Objective, item.OrderNumber, saved?.Status ?? "NotStarted", saved?.BestScore ?? 0,
                enrolled && (saved?.Status == "Completed" || missing.Length == 0), missing, active.FirstOrDefault(session => session.LessonId == item.Id && session.Mode == "Lesson")?.Id, saved is { BestScore: > 0 } || gradedLessonIds.Contains(item.Id));
        }).ToArray(), active, enrollment?.PlacementLevel?.Replace("Self:", "", StringComparison.Ordinal).Replace("Test:", "", StringComparison.Ordinal), recommendedUnitId);
    }
}
