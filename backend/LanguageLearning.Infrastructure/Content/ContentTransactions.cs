using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class ContentTransactions(LexiGoDbContext database, ContentAccess access, TimeProvider clock)
{
    public async Task<(ContentActor Actor, Lesson Lesson, Course Course, LessonVersion Version)?> LessonAsync(Guid userId, Guid familyId, Guid lessonId, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(userId, familyId, true, cancellationToken);
        if (actor is null) return null;
        var parent = await (from lesson in database.Lessons join unit in database.Units on lesson.UnitId equals unit.Id where lesson.Id == lessonId select new { unit.CourseId }).SingleOrDefaultAsync(cancellationToken);
        if (parent is null) return null;
        var course = await LockCourseAsync(parent.CourseId, cancellationToken);
        var lessonRow = await database.Lessons.SingleAsync(item => item.Id == lessonId, cancellationToken);
        var version = await database.LessonVersions.Where(item => item.LessonId == lessonId).OrderByDescending(item => item.VersionNumber).FirstAsync(cancellationToken);
        return new(actor, lessonRow, course!, version);
    }

    public Task<Course?> LockCourseAsync(Guid courseId, CancellationToken cancellationToken) =>
        database.Courses.FromSqlInterpolated($"SELECT * FROM \"Courses\" WHERE \"Id\" = {courseId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);

    public void Audit(Guid actorId, Guid lessonId, string action, string reason, string traceId) => database.AuditLogs.Add(new AuditLog
    {
        ActorId = actorId,
        EntityType = "Lesson",
        EntityId = lessonId.ToString(),
        Action = action,
        Reason = reason,
        TraceId = traceId,
        OccurredAt = clock.GetUtcNow()
    });
}
