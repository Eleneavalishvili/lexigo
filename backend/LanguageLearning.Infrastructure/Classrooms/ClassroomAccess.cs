using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record ClassActor(Guid Id, bool CanTeach, bool CanLearn);
public sealed record ClassroomOutcome(Guid? Id = null, string? Error = null, bool Forbidden = false,
    bool Missing = false, bool Created = false);

public sealed class ClassroomAccess(LexiGoDbContext database, LearningAccess learning, TimeProvider clock)
{
    public async Task<ClassActor?> ActorAsync(Guid userId, Guid familyId, bool forUpdate, CancellationToken cancellationToken)
    {
        if (forUpdate && !await learning.LockAsync(userId, familyId, cancellationToken)) return null;
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.Status != "Active" || !user.EmailConfirmed) return null;
        var family = await database.RefreshSessions.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId
            && item.FamilyId == familyId && item.RevokedAt == null && item.SecurityStamp == user.SecurityStamp
            && item.ExpiresAt > clock.GetUtcNow(), cancellationToken);
        if (family is null) return null;
        var roles = await (from membership in database.UserRoles
                           join role in database.Roles on membership.RoleId equals role.Id
                           where membership.UserId == userId
                           select role.Name).ToArrayAsync(cancellationToken);
        return new(userId, roles.Contains("Teacher") && user.TwoFactorEnabled && family.MfaVerified, roles.Contains("Learner"));
    }

    public async Task<Classroom?> LockAsync(Guid id, CancellationToken cancellationToken) =>
        await database.Classrooms.FromSqlInterpolated($"SELECT * FROM \"Classrooms\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public void Audit(Guid actorId, Guid classroomId, string action, string reason, string traceId)
    {
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId, EntityType = "Classroom", EntityId = classroomId.ToString(), Action = action,
            Reason = reason, TraceId = traceId, OccurredAt = clock.GetUtcNow()
        });
    }

    public static bool Text(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && !value.Contains('\0');
}
