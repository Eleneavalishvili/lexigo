using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed class ClassMemberService(LexiGoDbContext database, ClassroomAccess access)
{
    public async Task<ClassroomOutcome> ChangeAsync(Guid actorId, Guid familyId, Guid classroomId, Guid userId,
        bool restore, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null) return new(Forbidden: true);
        var classroom = await access.LockAsync(classroomId, cancellationToken);
        if (classroom is null) return new(Missing: true);
        bool teacher = actor.CanTeach && classroom.TeacherId == actorId;
        bool self = actor.CanLearn && userId == actorId && classroom.TeacherId != actorId;
        if ((!teacher && !self) || restore && !teacher) return new(Missing: true);
        if (userId == classroom.TeacherId) return new(Error: "The teacher is not a learner member.");
        var member = await database.ClassMemberships.SingleOrDefaultAsync(item => item.ClassroomId == classroomId && item.UserId == userId, cancellationToken);
        if (member is null) return new(Missing: true);
        if (self && member.Status == "Removed") return new(Missing: true);
        if (restore)
        {
            if (classroom.IsArchived || member.Status != "Removed") return new(Error: "Only a teacher-removed membership can be restored in an active class.");
            if (!await database.Users.AnyAsync(user => user.Id == userId && user.Status == "Active" && user.EmailConfirmed, cancellationToken))
                return new(Error: "This account cannot currently join the class.");
            if (!await (from roleMembership in database.UserRoles
                        join role in database.Roles on roleMembership.RoleId equals role.Id
                        where roleMembership.UserId == userId && role.Name == "Learner"
                        select roleMembership.UserId).AnyAsync(cancellationToken)
                || !await database.Courses.AnyAsync(course => course.Id == classroom.CourseId && course.IsPublished, cancellationToken))
                return new(Error: "This learner or course is not available for membership restoration.");
            if (await database.ClassMemberships.CountAsync(item => item.UserId == userId && item.Status == "Active", cancellationToken) >= 50)
                return new(Error: "This learner has reached the active-class limit.");
            if (await database.ClassMemberships.CountAsync(item => item.ClassroomId == classroomId && item.Status == "Active", cancellationToken) >= 200)
                return new(Error: "This class has reached its 200-member limit.");
            member.Status = "Active";
        }
        else
        {
            if (member.Status != "Active") return new(classroomId);
            if (teacher && classroom.IsArchived) return new(Error: "Archived classes are read-only.");
            member.Status = teacher ? "Removed" : "Left";
        }
        access.Audit(actorId, classroomId, restore ? "ClassMemberRestored" : teacher ? "ClassMemberRemoved" : "ClassMemberLeft",
            "Membership status changed for " + userId, traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(classroomId);
    }
}
