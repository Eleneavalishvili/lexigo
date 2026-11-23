using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class LessonDraftErasure(LexiGoDbContext database, ContentAccess access,
    ContentTransactions content, TimeProvider clock)
{
    public async Task<ContentOutcome> EraseAsync(Guid actorId, Guid familyId, Guid lessonId,
        Guid versionId, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null) return new(Forbidden: true);
        var parent = await (from lesson in database.Lessons
                            join unit in database.Units on lesson.UnitId equals unit.Id
                            where lesson.Id == lessonId
                            select new { unit.CourseId }).SingleOrDefaultAsync(cancellationToken);
        if (parent is null) return new(Forbidden: true);
        var course = await content.LockCourseAsync(parent.CourseId, cancellationToken);
        var version = await database.LessonVersions.SingleOrDefaultAsync(
            item => item.Id == versionId && item.LessonId == lessonId, cancellationToken);
        if (course is null || version is null || !(actor.IsAdmin ||
            actor.Id == version.CreatedBy && actor.CanAuthor(course.TargetLanguageId)))
            return new(Forbidden: true);
        if (version.PrivateTextErasedAt is not null) return new(version.Id);

        var lessonRow = await database.Lessons.SingleAsync(item => item.Id == lessonId, cancellationToken);
        if (version.PublishedAt is not null || version.Status == "Published" || lessonRow.CurrentVersionId == versionId)
            return new(Error: "Published lesson history cannot be erased with this action.");
        var exercises = database.Exercises.Where(item => item.LessonVersionId == versionId);
        if (await database.LearningSessions.AnyAsync(item => item.LessonVersionId == versionId, cancellationToken)
            || await database.SessionExercises.AnyAsync(item => exercises.Any(exercise => exercise.Id == item.ExerciseId), cancellationToken)
            || await database.ContentReports.AnyAsync(item => item.VersionId == versionId, cancellationToken))
            return new(Error: "This version has learning or report history. Transfer it for retention review.");

        await database.AcceptedAnswers.Where(item => exercises.Any(exercise => exercise.Id == item.ExerciseId))
            .ExecuteDeleteAsync(cancellationToken);
        await database.ExerciseOptions.Where(item => exercises.Any(exercise => exercise.Id == item.ExerciseId))
            .ExecuteDeleteAsync(cancellationToken);
        await exercises.ExecuteDeleteAsync(cancellationToken);
        await database.ContentReviews.Where(item => item.LessonVersionId == versionId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Comment, "Private review text erased."), cancellationToken);
        version.Title = "Erased private draft";
        version.Objective = "Private draft text erased.";
        version.Explanation = "";
        version.Status = "Archived";
        version.PrivateTextErasedAt = clock.GetUtcNow();
        bool otherText = await database.LessonVersions.AnyAsync(item => item.LessonId == lessonId &&
            item.Id != versionId && item.PrivateTextErasedAt == null, cancellationToken);
        if (lessonRow.CurrentVersionId is null && !otherText)
        {
            lessonRow.Title = version.Title;
            lessonRow.Objective = version.Objective;
            lessonRow.Status = "Archived";
        }
        content.Audit(actorId, lessonId, "LessonPrivateTextErased",
            $"Private draft version {versionId} erased after confirmation.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(versionId);
    }
}
