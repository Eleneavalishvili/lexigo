using System.Security.Cryptography;
using System.Text;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record InvitationCreated(string Code, DateTimeOffset ExpiresAt, int MaxUses);
public sealed record InvitationOutcome(InvitationCreated? Value = null, string? Error = null, bool Missing = false, bool Forbidden = false);

public sealed class ClassInvitationService(LexiGoDbContext database, ClassroomAccess access, TimeProvider clock)
{
    public async Task<InvitationOutcome> RotateAsync(Guid actorId, Guid familyId, Guid id, int days, int maxUses,
        string traceId, CancellationToken cancellationToken)
    {
        if (days is < 1 or > 30 || maxUses is < 1 or > 200) return new(Error: "Use 1–30 days and 1–200 joins.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await access.ActorAsync(actorId, familyId, true, cancellationToken) is not { CanTeach: true }) return new(Forbidden: true);
        var classroom = await access.LockAsync(id, cancellationToken);
        if (classroom?.TeacherId != actorId) return new(Missing: true);
        if (classroom.IsArchived || !await database.Courses.AnyAsync(item => item.Id == classroom.CourseId && item.IsPublished, cancellationToken))
            return new(Error: "This class is not accepting new members.");
        var now = clock.GetUtcNow();
        await database.ClassInvitations.Where(item => item.ClassroomId == id && item.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, now), cancellationToken);
        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(20)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var invitation = new ClassInvitation
        {
            ClassroomId = id, CodeHash = Hash(code), ExpiresAt = now.AddDays(days), MaxUses = maxUses
        };
        database.ClassInvitations.Add(invitation);
        access.Audit(actorId, id, "ClassInvitationRotated", "Earlier codes revoked; new code expires " + invitation.ExpiresAt.ToString("O"), traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(new(code, invitation.ExpiresAt, maxUses));
    }

    internal static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
