using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record CreateAssignmentInput(
    [Required, MaxLength(120)] string Title,
    DateTimeOffset DueAt,
    [Range(0, 100)] int MinimumScore,
    [Required, MinLength(1), MaxLength(20)] Guid[] LessonIds,
    Guid RequestKey);

public sealed record EditAssignmentInput(
    [Required, MaxLength(120)] string Title,
    DateTimeOffset DueAt,
    [property: JsonRequired]
    [Range(0, 100)] int MinimumScore,
    [Required, MinLength(1), MaxLength(20)] Guid[] LessonIds,
    [Range(1, int.MaxValue)] int Revision);

public sealed class AssignmentManagement(LexiGoDbContext database, ClassroomAccess access, TimeProvider clock)
{
    public async Task<ClassroomOutcome> CreateAsync(Guid userId, Guid familyId, Guid classId,
        CreateAssignmentInput input, string traceId, CancellationToken cancellationToken)
    {
        if (!Valid(input.Title, input.MinimumScore, input.LessonIds) || input.RequestKey == Guid.Empty)
        {
            return new(Error: "Enter a title, minimum score and up to 20 different lessons.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(userId, familyId, true, cancellationToken);
        if (actor is not { CanTeach: true }) return new(Forbidden: true);
        var classroom = await access.LockAsync(classId, cancellationToken);
        if (classroom?.TeacherId != userId) return new(Missing: true);

        var title = input.Title.Trim();
        var dueAt = input.DueAt.ToUniversalTime();
        var lessonIds = input.LessonIds.Order().ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { title, dueAt, input.MinimumScore, lessonIds })));
        var existing = await database.Assignments.SingleOrDefaultAsync(
            item => item.ClassroomId == classId && item.RequestKey == input.RequestKey, cancellationToken);
        if (existing is not null)
        {
            return existing.RequestFingerprint == fingerprint
                ? new(existing.Id)
                : new(Error: "This request was already used to create an assignment with different details.");
        }

        if (classroom.IsArchived) return new(Error: "Archived classes are read-only.");
        var now = clock.GetUtcNow();
        if (dueAt <= now) return new(Error: "Choose a deadline in the future.");

        if (!await AllPublishedAsync(classroom.CourseId, lessonIds, cancellationToken))
            return new(Error: "Choose published lessons from this class's course.");

        var assignment = new Assignment
        {
            ClassroomId = classId,
            Title = title,
            CreatedAt = now,
            DueAt = dueAt,
            MinimumScore = input.MinimumScore,
            RequestKey = input.RequestKey,
            RequestFingerprint = fingerprint
        };
        database.Assignments.Add(assignment);
        database.AssignmentLessons.AddRange(lessonIds.Select(id => new AssignmentLesson
        {
            AssignmentId = assignment.Id,
            LessonId = id
        }));
        var recipients = await (from member in database.ClassMemberships
                                join user in database.Users on member.UserId equals user.Id
                                where member.ClassroomId == classId && member.Status == "Active"
                                    && user.Status == "Active" && user.EmailConfirmed
                                select member.UserId).ToArrayAsync(cancellationToken);
        database.Notifications.AddRange(recipients.Select(recipient => new Notification
        {
            AssignmentId = assignment.Id,
            UserId = recipient,
            Type = "AssignmentCreated",
            Title = "New class assignment",
            Body = assignment.Title,
            CreatedAt = now
        }));
        access.Audit(userId, classId, "AssignmentCreated", assignment.Id.ToString(), traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(assignment.Id, Created: true);
    }

    public async Task<ClassroomOutcome> EditAsync(Guid userId, Guid familyId, Guid id,
        EditAssignmentInput input, string traceId, CancellationToken cancellationToken)
    {
        if (!Valid(input.Title, input.MinimumScore, input.LessonIds) || input.Revision < 1)
            return new(Error: "Enter a title, minimum score and up to 20 different lessons.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(userId, familyId, true, cancellationToken);
        if (actor is not { CanTeach: true }) return new(Forbidden: true);
        var classId = await database.Assignments.AsNoTracking().Where(item => item.Id == id)
            .Select(item => (Guid?)item.ClassroomId).SingleOrDefaultAsync(cancellationToken);
        if (classId is null) return new(Missing: true);
        var classroom = await access.LockAsync(classId.Value, cancellationToken);
        if (classroom?.TeacherId != userId) return new(Missing: true);
        if (classroom.IsArchived) return new(Error: "Archived classes are read-only.");

        var assignment = await database.Assignments.SingleAsync(item => item.Id == id, cancellationToken);
        if (assignment.Revision != input.Revision) return new(Error: "This assignment changed. Refresh it before editing.");
        var dueAt = input.DueAt.ToUniversalTime();
        if (dueAt != assignment.DueAt && dueAt <= clock.GetUtcNow())
            return new(Error: "Choose a new deadline in the future.");
        var links = await database.AssignmentLessons.Where(item => item.AssignmentId == id)
            .ToArrayAsync(cancellationToken);
        bool lessonsChanged = !input.LessonIds.ToHashSet().SetEquals(links.Select(link => link.LessonId));
        bool gradingChanged = lessonsChanged || dueAt != assignment.DueAt || input.MinimumScore != assignment.MinimumScore;
        if (gradingChanged && await database.AssignmentSubmissions.AnyAsync(item => item.AssignmentId == id, cancellationToken))
            return new(Error: "The deadline, score and lessons are fixed once work is submitted. You can still update the title.");
        if (lessonsChanged && !await AllPublishedAsync(classroom.CourseId, input.LessonIds, cancellationToken))
            return new(Error: "Choose published lessons from this class's course.");
        database.AssignmentLessons.RemoveRange(links.Where(link => !input.LessonIds.Contains(link.LessonId)));
        database.AssignmentLessons.AddRange(input.LessonIds.Except(links.Select(link => link.LessonId))
            .Select(lessonId => new AssignmentLesson { AssignmentId = id, LessonId = lessonId }));
        assignment.Title = input.Title.Trim();
        assignment.MinimumScore = input.MinimumScore;
        assignment.DueAt = dueAt;
        assignment.Revision++;
        access.Audit(userId, classroom.Id, "AssignmentEdited", id.ToString(), traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(id);
    }

    private static bool Valid(string title, int minimumScore, Guid[] lessonIds) =>
        ClassroomAccess.Text(title, 120) && minimumScore is >= 0 and <= 100
        && lessonIds is { Length: >= 1 and <= 20 } && lessonIds.Distinct().Count() == lessonIds.Length;

    private async Task<bool> AllPublishedAsync(Guid courseId, Guid[] lessonIds, CancellationToken cancellationToken)
    {
        var publishedLessons = from lesson in database.Lessons
                               join unit in database.Units on lesson.UnitId equals unit.Id
                               join course in database.Courses on unit.CourseId equals course.Id
                               where course.Id == courseId && course.IsPublished
                                   && lesson.Status == "Published" && lesson.CurrentVersionId != null
                                   && lessonIds.Contains(lesson.Id)
                               select lesson.Id;
        return await publishedLessons.CountAsync(cancellationToken) == lessonIds.Length;
    }
}
