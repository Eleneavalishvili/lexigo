using System.Security.Cryptography;
using System.Text.Json;
using LanguageLearning.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record ClassTransferLink(string Token, DateTimeOffset ExpiresAt);
public sealed record ClassTransferPreview(string ClassName, DateTimeOffset ExpiresAt);
internal sealed record ClassTransferSubject(Guid ClassroomId, Guid OwnerId, Guid RecipientId, int Revision,
    DateTimeOffset ExpiresAt);

public sealed class ClassroomTransferService(LexiGoDbContext database, ClassroomAccess access,
    IDataProtectionProvider protection, TimeProvider clock)
{
    private readonly IDataProtector protector = protection.CreateProtector("LexiGo.ClassTransfer.v1");

    public async Task<ClassTransferLink?> CreateAsync(Guid userId, Guid familyId, Guid classId,
        string email, string traceId, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        if (await access.ActorAsync(userId, familyId, true, token) is not { CanTeach: true }) return null;
        var classroom = await access.LockAsync(classId, token);
        if (classroom is null || classroom.TeacherId != userId || classroom.IsArchived) return null;
        string normalized = email.Trim().ToUpperInvariant();
        var recipient = await (from user in database.Users
                               join membership in database.UserRoles on user.Id equals membership.UserId
                               join role in database.Roles on membership.RoleId equals role.Id
                               where user.NormalizedEmail == normalized && user.Id != userId && user.Status == "Active"
                                   && user.EmailConfirmed && user.TwoFactorEnabled && role.Name == "Teacher"
                               select user.Id).SingleOrDefaultAsync(token);
        if (recipient == Guid.Empty) return null;
        var expires = clock.GetUtcNow().AddHours(24);
        string link = protector.Protect(JsonSerializer.Serialize(new ClassTransferSubject(
            classId, userId, recipient, classroom.Revision, expires)));
        access.Audit(userId, classId, "ClassroomTransferOffered",
            "A specific teacher was offered the class for 24 hours.", traceId);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(link, expires);
    }

    public async Task<ClassTransferPreview?> PreviewAsync(Guid userId, Guid familyId, string token,
        CancellationToken cancellationToken)
    {
        if (Read(token) is not { } subject || subject.RecipientId != userId) return null;
        if (await access.ActorAsync(userId, familyId, false, cancellationToken) is not { CanTeach: true }) return null;
        return await database.Classrooms.AsNoTracking().Where(item => item.Id == subject.ClassroomId
            && item.TeacherId == subject.OwnerId && item.Revision == subject.Revision && !item.IsArchived)
            .Select(item => new ClassTransferPreview(item.Name, subject.ExpiresAt)).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ClassroomOutcome> AcceptAsync(Guid userId, Guid familyId, string token,
        string traceId, CancellationToken cancellationToken)
    {
        if (Read(token) is not { } subject || subject.RecipientId != userId) return new(Missing: true);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await access.ActorAsync(userId, familyId, true, cancellationToken) is not { CanTeach: true })
            return new(Forbidden: true);
        var classroom = await access.LockAsync(subject.ClassroomId, cancellationToken);
        if (classroom is null || classroom.IsArchived) return new(Missing: true);
        if (classroom.TeacherId == userId) return new(classroom.Id);
        if (classroom.TeacherId != subject.OwnerId || classroom.Revision != subject.Revision)
            return new(Error: "This handover changed. Ask the class teacher for a new link.");
        if (await database.Classrooms.CountAsync(item => item.TeacherId == userId && !item.IsArchived, cancellationToken) >= 50)
            return new(Error: "Archive an old class before accepting another active class.");
        classroom.TeacherId = userId;
        classroom.CreationKey = Guid.NewGuid();
        classroom.Revision++;
        await database.ClassInvitations.Where(item => item.ClassroomId == classroom.Id && item.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, clock.GetUtcNow()), cancellationToken);
        access.Audit(userId, classroom.Id, "ClassroomTransferred",
            "The designated teacher accepted the class handover.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(classroom.Id);
    }

    private ClassTransferSubject? Read(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return null;
        try
        {
            var subject = JsonSerializer.Deserialize<ClassTransferSubject>(protector.Unprotect(token));
            return subject?.ExpiresAt > clock.GetUtcNow() ? subject : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException)
        {
            return null;
        }
    }
}
