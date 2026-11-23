using System.ComponentModel.DataAnnotations;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record WriteAssignmentFeedback(Guid SubmissionId, [Required, MaxLength(1000)] string Message);
public sealed record AssignmentFeedbackView(string Message, DateTimeOffset CreatedAt);
public sealed record LearnerFeedbackView(Guid LessonId, string Message, DateTimeOffset CreatedAt, int Score);

public sealed class AssignmentFeedback(LexiGoDbContext database, ClassroomAccess access, TimeProvider clock)
{
    public async Task<ClassroomOutcome> WriteAsync(Guid userId, Guid familyId, Guid assignmentId,
        WriteAssignmentFeedback input, string traceId, CancellationToken cancellationToken)
    {
        if (!ClassroomAccess.Text(input.Message, 1000)) return new(Error: "Enter a feedback message.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await access.ActorAsync(userId, familyId, true, cancellationToken) is not { CanTeach: true })
            return new(Forbidden: true);

        var classroomId = await database.Assignments.Where(item => item.Id == assignmentId)
            .Select(item => (Guid?)item.ClassroomId).SingleOrDefaultAsync(cancellationToken);
        if (classroomId is null) return new(Missing: true);
        var classroom = await access.LockAsync(classroomId.Value, cancellationToken);
        if (classroom?.TeacherId != userId) return new(Missing: true);

        var submission = await database.AssignmentSubmissions.AsNoTracking().SingleOrDefaultAsync(
            item => item.AssignmentId == assignmentId && item.Id == input.SubmissionId, cancellationToken);
        if (submission is null) return new(Missing: true);

        var message = input.Message.Trim();
        var saved = await database.TeacherFeedback.AsNoTracking().SingleOrDefaultAsync(
            item => item.SubmissionId == submission.Id, cancellationToken);
        if (saved is not null)
            return saved.Message == message ? new(saved.Id) : new(Error: "Feedback has already been posted for this attempt.");
        if (classroom.IsArchived) return new(Error: "Archived classes are read-only.");
        if (!await database.ClassMemberships.AnyAsync(item => item.ClassroomId == classroom.Id
            && item.UserId == submission.UserId && item.Status == "Active", cancellationToken))
            return new(Error: "Feedback can only be posted for current class members.");

        var feedback = new TeacherFeedback
        {
            SubmissionId = submission.Id,
            TeacherId = userId,
            Message = message,
            CreatedAt = clock.GetUtcNow()
        };
        database.TeacherFeedback.Add(feedback);
        database.Notifications.Add(new Notification
        {
            UserId = submission.UserId,
            Type = "AssignmentFeedback",
            Title = "New assignment feedback",
            Body = "Open your class assignment to read your teacher's feedback.",
            CreatedAt = feedback.CreatedAt
        });
        access.Audit(userId, classroom.Id, "AssignmentFeedbackPosted", submission.Id.ToString(), traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(feedback.Id);
    }
}
