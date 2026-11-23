using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed class ClassJoiningService(LexiGoDbContext database, ClassroomAccess access, TimeProvider clock)
{
    public async Task<ClassroomOutcome> JoinAsync(Guid actorId, Guid familyId, string code, string traceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is < 20 or > 64) return new(Missing: true);
        string hash = ClassInvitationService.Hash(code.Trim());
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await access.ActorAsync(actorId, familyId, true, cancellationToken) is not { CanLearn: true }) return new(Forbidden: true);
        var target = await database.ClassInvitations.Where(item => item.CodeHash == hash)
            .Select(item => new { item.Id, item.ClassroomId }).SingleOrDefaultAsync(cancellationToken);
        if (target is null) return new(Missing: true);
        var classroom = await access.LockAsync(target.ClassroomId, cancellationToken);
        if (classroom is null) return new(Missing: true);
        if (classroom.TeacherId == actorId) return new(Error: "You already manage this class.");
        var member = await database.ClassMemberships.SingleOrDefaultAsync(item => item.ClassroomId == classroom.Id && item.UserId == actorId, cancellationToken);
        if (member?.Status == "Active") return new(classroom.Id);
        if (member?.Status == "Removed") return new(Error: "Ask your teacher to restore your membership.");
        var invitation = await database.ClassInvitations.SingleAsync(item => item.Id == target.Id, cancellationToken);
        var now = clock.GetUtcNow();
        if (classroom.IsArchived || invitation.RevokedAt != null || invitation.ExpiresAt <= now || invitation.UsedCount >= invitation.MaxUses)
            return new(Missing: true);
        if (!await database.Courses.AnyAsync(item => item.Id == classroom.CourseId && item.IsPublished, cancellationToken)) return new(Missing: true);
        if (await database.ClassMemberships.CountAsync(item => item.ClassroomId == classroom.Id && item.Status == "Active", cancellationToken) >= 200)
            return new(Error: "This class has reached its 200-member limit.");
        if (await database.ClassMemberships.CountAsync(item => item.UserId == actorId && item.Status == "Active", cancellationToken) >= 50)
            return new(Error: "Leave an old class before joining more than 50 active classes.");
        if (member is null)
        {
            member = new ClassMembership { ClassroomId = classroom.Id, UserId = actorId };
            database.ClassMemberships.Add(member);
        }
        member.Status = "Active";
        member.JoinedAt = now;
        invitation.UsedCount++;
        if (!await database.Enrollments.AnyAsync(item => item.UserId == actorId && item.CourseId == classroom.CourseId, cancellationToken))
            database.Enrollments.Add(new Enrollment { UserId = actorId, CourseId = classroom.CourseId, EnrolledAt = now });
        access.Audit(actorId, classroom.Id, "ClassMemberJoined", "Learner joined using a valid invitation.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(classroom.Id);
    }
}
