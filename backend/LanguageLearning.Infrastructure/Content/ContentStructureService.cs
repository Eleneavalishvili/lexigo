using LanguageLearning.Core.Content;
using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class ContentStructureService(LexiGoDbContext database, ContentAccess access, ContentTransactions content, TimeProvider clock)
{
    public async Task<ContentOutcome> EditCourseAsync(Guid actorId, Guid familyId, Guid courseId,
        string title, string description, string traceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 120 || title.Contains('\0')
            || string.IsNullOrWhiteSpace(description) || description.Length > 1000 || description.Contains('\0'))
            return new(Error: "Check the course title and description.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        var course = await content.LockCourseAsync(courseId, cancellationToken);
        if (course is null) return new(Error: "Course not found.");
        course.Title = title.Trim();
        course.Description = description.Trim();
        AuditCourse(actorId, courseId, "CourseEdited", "Course title and description changed.", traceId, clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(courseId);
    }

    public async Task<ContentOutcome> MoveCourseAsync(Guid actorId, Guid familyId, Guid courseId,
        int fromPosition, int toPosition, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        // Course ordering spans the whole catalog. Lock these rows in a consistent order.
        var courses = await database.Courses.FromSqlRaw("SELECT * FROM \"Courses\" ORDER BY \"Id\" FOR UPDATE")
            .ToArrayAsync(cancellationToken);
        var course = courses.SingleOrDefault(item => item.Id == courseId);
        if (course is null) return new(Error: "Course not found.");
        if (course.OrderNumber != fromPosition)
            return new(Error: "The course order changed. Reload the workshop and try again.");
        if (fromPosition == toPosition) return new(courseId);
        var occupied = courses.Where(item => item.Id != courseId && item.OrderNumber == toPosition).ToArray();
        if (occupied.Length > 1) return new(Error: "More than one course has that position.");

        if (occupied.Length == 1)
        {
            occupied[0].OrderNumber = fromPosition;
            AuditCourse(actorId, occupied[0].Id, "CourseMoved", "Course position swapped.", traceId, clock.GetUtcNow());
        }
        course.OrderNumber = toPosition;
        AuditCourse(actorId, courseId, "CourseMoved", "Course position changed.", traceId, clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(courseId);
    }

    public async Task<ContentOutcome> SetCourseArchiveAsync(Guid actorId, Guid familyId, Guid courseId,
        bool archived, string reason, string traceId, CancellationToken cancellationToken)
    {
        if (reason.Trim().Length is < 3 or > 500 || reason.Contains('\0'))
            return new(Error: "Give a reason between 3 and 500 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        var course = await content.LockCourseAsync(courseId, cancellationToken);
        if (course is null) return new(Error: "Course not found.");
        if (course.ArchivedAt.HasValue == archived) return new(courseId);

        var now = clock.GetUtcNow();
        course.ArchivedAt = archived ? now : null;
        course.IsPublished = !archived && await database.Lessons.AnyAsync(lesson =>
            lesson.Status == "Published" && lesson.CurrentVersionId != null &&
            database.Units.Any(unit => unit.Id == lesson.UnitId && unit.CourseId == courseId && unit.ArchivedAt == null), cancellationToken);
        AuditCourse(actorId, courseId, archived ? "CourseArchived" : "CourseRestored", reason.Trim(), traceId, now);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(courseId);
    }

    public async Task<ContentOutcome> SetUnitArchiveAsync(Guid actorId, Guid familyId, Guid unitId,
        bool archived, string reason, string traceId, CancellationToken cancellationToken)
    {
        if (reason.Trim().Length is < 3 or > 500 || reason.Contains('\0'))
            return new(Error: "Give a reason between 3 and 500 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        var courseId = await database.Units.Where(unit => unit.Id == unitId)
            .Select(unit => (Guid?)unit.CourseId).SingleOrDefaultAsync(cancellationToken);
        if (courseId is null) return new(Error: "Unit not found.");
        var course = await content.LockCourseAsync(courseId.Value, cancellationToken);
        var unit = await database.Units.SingleOrDefaultAsync(item => item.Id == unitId, cancellationToken);
        if (course is null || unit is null) return new(Error: "Unit not found.");
        if (unit.ArchivedAt.HasValue == archived) return new(unitId);

        var published = database.Lessons.Where(lesson => lesson.Status == "Published" && lesson.CurrentVersionId != null);
        var links = from edge in database.LessonPrerequisites
                    join prerequisite in published on edge.PrerequisiteLessonId equals prerequisite.Id
                    join dependent in published on edge.LessonId equals dependent.Id
                    join sourceUnit in database.Units on prerequisite.UnitId equals sourceUnit.Id
                    join targetUnit in database.Units on dependent.UnitId equals targetUnit.Id
                    where sourceUnit.Id != targetUnit.Id
                    select new { SourceId = sourceUnit.Id, SourceArchivedAt = sourceUnit.ArchivedAt,
                        TargetId = targetUnit.Id, TargetArchivedAt = targetUnit.ArchivedAt };
        if (archived && await links.AnyAsync(link => link.SourceId == unitId && link.TargetArchivedAt == null, cancellationToken))
            return new(Error: "Archive units with dependent published lessons first.");
        if (!archived && await links.AnyAsync(link => link.TargetId == unitId && link.SourceArchivedAt != null, cancellationToken))
            return new(Error: "Restore units containing prerequisite lessons first.");

        unit.ArchivedAt = archived ? clock.GetUtcNow() : null;
        bool otherVisible = await published.AnyAsync(lesson => database.Units.Any(other =>
            other.Id == lesson.UnitId && other.CourseId == course.Id && other.Id != unitId && other.ArchivedAt == null), cancellationToken);
        course.IsPublished = !course.ArchivedAt.HasValue &&
            (otherVisible || !archived && await published.AnyAsync(lesson => lesson.UnitId == unitId, cancellationToken));
        AuditUnit(actorId, unitId, archived ? "UnitArchived" : "UnitRestored", reason.Trim(), traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(unitId);
    }

    public async Task<ContentOutcome> CreateUnitAsync(Guid actorId, Guid familyId, Guid courseId, UnitInput input, string traceId, CancellationToken cancellationToken)
    {
        if (!ValidUnit(input)) return new(Error: "Check the unit fields.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        if (await content.LockCourseAsync(courseId, cancellationToken) is null) return new(Error: "Course not found.");
        if (await database.Units.AnyAsync(item => item.CourseId == courseId && item.OrderNumber == input.OrderNumber, cancellationToken)) return new(Error: "This unit position is occupied.");
        var unit = new Unit { CourseId = courseId, Title = input.Title.Trim(), Description = input.Description, OrderNumber = input.OrderNumber, Difficulty = input.Difficulty };
        database.Units.Add(unit);
        AuditUnit(actorId, unit.Id, "UnitCreated", "Course unit created.", traceId);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(unit.Id);
    }

    public async Task<ContentOutcome> EditUnitAsync(Guid actorId, Guid familyId, Guid unitId,
        UnitInput input, string traceId, CancellationToken cancellationToken)
    {
        if (!ValidUnit(input)) return new(Error: "Check the unit fields.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        var courseId = await database.Units.Where(item => item.Id == unitId)
            .Select(item => (Guid?)item.CourseId).SingleOrDefaultAsync(cancellationToken);
        if (courseId is null || await content.LockCourseAsync(courseId.Value, cancellationToken) is null)
            return new(Error: "Unit not found.");
        var unit = await database.Units.SingleOrDefaultAsync(item => item.Id == unitId, cancellationToken);
        if (unit is null) return new(Error: "Unit not found.");
        if (await database.Units.AnyAsync(item => item.CourseId == courseId
            && item.Id != unitId && item.OrderNumber == input.OrderNumber, cancellationToken))
            return new(Error: "This unit position is occupied.");

        unit.Title = input.Title.Trim();
        unit.Description = input.Description;
        unit.Difficulty = input.Difficulty;
        unit.OrderNumber = input.OrderNumber;
        AuditUnit(actorId, unit.Id, "UnitEdited", "Course unit details changed.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(unit.Id);
    }

    public async Task<ContentOutcome> MoveUnitAsync(Guid actorId, Guid familyId, Guid unitId,
        int fromPosition, int toPosition, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        var courseId = await database.Units.Where(item => item.Id == unitId)
            .Select(item => (Guid?)item.CourseId).SingleOrDefaultAsync(cancellationToken);
        if (courseId is null || await content.LockCourseAsync(courseId.Value, cancellationToken) is null)
            return new(Error: "Unit not found.");
        var units = await database.Units.Where(item => item.CourseId == courseId).ToArrayAsync(cancellationToken);
        var unit = units.Single(item => item.Id == unitId);
        if (unit.OrderNumber != fromPosition)
            return new(Error: "The unit order changed. Reload the workshop and try again.");
        if (fromPosition == toPosition) return new(unitId);

        var neighbor = units.SingleOrDefault(item => item.OrderNumber == toPosition);
        if (neighbor is not null)
        {
            var occupied = units.Select(item => item.OrderNumber).ToHashSet();
            unit.OrderNumber = Enumerable.Range(1, units.Length + 1).First(number => !occupied.Contains(number));
            await database.SaveChangesAsync(cancellationToken);
            neighbor.OrderNumber = fromPosition;
            await database.SaveChangesAsync(cancellationToken);
            AuditUnit(actorId, neighbor.Id, "UnitMoved", "Unit position swapped within its course.", traceId);
        }
        unit.OrderNumber = toPosition;
        AuditUnit(actorId, unitId, "UnitMoved", "Unit position changed within its course.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(unitId);
    }

    private static bool ValidUnit(UnitInput input) =>
        !string.IsNullOrWhiteSpace(input.Title) && input.Title.Length <= 160
        && !string.IsNullOrWhiteSpace(input.Description) && input.Description.Length <= 3000
        && !input.Title.Contains('\0') && !input.Description.Contains('\0')
        && input.OrderNumber is >= 1 and <= 10000
        && input.Difficulty is "Beginner" or "Intermediate" or "Advanced" or "A1" or "A2" or "B1" or "B2" or "C1" or "C2";

    private void AuditUnit(Guid actorId, Guid unitId, string action, string reason, string traceId) =>
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            EntityType = "Unit",
            EntityId = unitId.ToString(),
            Action = action,
            Reason = reason,
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });

    private void AuditCourse(Guid actorId, Guid courseId, string action, string reason, string traceId, DateTimeOffset at) =>
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            EntityType = "Course",
            EntityId = courseId.ToString(),
            Action = action,
            Reason = reason,
            TraceId = traceId,
            OccurredAt = at
        });
}
