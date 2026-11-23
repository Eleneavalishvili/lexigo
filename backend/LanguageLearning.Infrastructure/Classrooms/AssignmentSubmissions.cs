using System.Text.Json.Serialization;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record SubmitAssignmentInput([property: JsonRequired] Guid SessionId);
public sealed record AssignmentReceipt(Guid Id, Guid SessionId, DateTimeOffset SubmittedAt, bool IsLate);
public sealed record AssignmentSubmissionOutcome(AssignmentReceipt? Receipt = null, string? Error = null,
    bool Forbidden = false, bool Missing = false);

public sealed class AssignmentSubmissions(LexiGoDbContext database, ClassroomAccess access, TimeProvider clock)
{
    public async Task<AssignmentSubmissionOutcome> SubmitAsync(Guid userId, Guid familyId, Guid assignmentId,
        Guid sessionId, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty) return new(Error: "Choose a completed lesson attempt.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(userId, familyId, true, cancellationToken);
        if (actor is not { CanLearn: true }) return new(Forbidden: true);
        var classId = await database.Assignments.Where(item => item.Id == assignmentId)
            .Select(item => (Guid?)item.ClassroomId).SingleOrDefaultAsync(cancellationToken);
        if (classId is null) return new(Missing: true);
        var classroom = await access.LockAsync(classId.Value, cancellationToken);
        if (classroom is null) return new(Missing: true);
        var membership = await database.ClassMemberships.AsNoTracking().SingleOrDefaultAsync(
            item => item.ClassroomId == classId && item.UserId == userId && item.Status == "Active", cancellationToken);
        if (membership is null) return new(Missing: true);

        var saved = await database.AssignmentSubmissions.AsNoTracking().SingleOrDefaultAsync(
            item => item.AssignmentId == assignmentId && item.UserId == userId && item.SessionId == sessionId, cancellationToken);
        if (saved is not null) return new(Receipt(saved));
        if (classroom.IsArchived) return new(Error: "Archived classes are read-only.");

        var assignment = await database.Assignments.AsNoTracking().SingleAsync(item => item.Id == assignmentId, cancellationToken);
        var eligible = Eligible(database, assignmentId, userId, classroom.CourseId, assignment.CreatedAt, membership.JoinedAt);
        if (!await eligible.AnyAsync(item => item.Id == sessionId, cancellationToken))
            return new(Error: "Choose your own completed graded attempt for an assigned lesson, started after joining and after the assignment was created.");

        var now = clock.GetUtcNow();
        // Match PostgreSQL timestamp precision so the first receipt and retries agree.
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
        var submission = new AssignmentSubmission
        {
            AssignmentId = assignmentId,
            UserId = userId,
            SessionId = sessionId,
            SubmittedAt = now,
            IsLate = now > assignment.DueAt
        };
        database.AssignmentSubmissions.Add(submission);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Receipt(submission));
    }

    internal static IQueryable<LearningSession> Eligible(LexiGoDbContext database, Guid assignmentId, Guid userId,
        Guid courseId, DateTimeOffset createdAt, DateTimeOffset joinedAt) =>
        database.LearningSessions.AsNoTracking().Where(session => session.UserId == userId
            && session.CourseId == courseId && session.CompletedAt != null
            && session.StartedAt >= createdAt && session.StartedAt >= joinedAt && session.PlacementMap == null
            && database.AssignmentLessons.Any(link => link.AssignmentId == assignmentId && link.LessonId == session.LessonId)
            && (from exercise in database.SessionExercises
                join attempt in database.AnswerAttempts on exercise.Id equals attempt.SessionExerciseId
                where exercise.SessionId == session.Id && attempt.UserId == userId && attempt.IsGraded
                select attempt.Id).Any());

    private static AssignmentReceipt Receipt(AssignmentSubmission item) =>
        new(item.Id, item.SessionId, item.SubmittedAt, item.IsLate);
}
