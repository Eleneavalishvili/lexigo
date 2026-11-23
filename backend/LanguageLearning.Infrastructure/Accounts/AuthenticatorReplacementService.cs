using System.Text.Json;
using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class AuthenticatorReplacementService(LexiGoDbContext database, UserManager<ApplicationUser> users,
    SessionService sessions, MfaCodeVerifier codes, TimeProvider clock)
{
    private const string PendingName = "PendingAuthenticatorReplacement";

    public async Task<ReplacementSetup?> BeginAsync(Guid userId, Guid familyId, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAsync(userId, cancellationToken);
        if (!await PasswordMatchesAsync(user, password))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (!await SessionMatchesAsync(user!, familyId, cancellationToken)) return null;
        var startedAt = await database.RefreshSessions.Where(item => item.UserId == userId && item.FamilyId == familyId)
            .MinAsync(item => item.CreatedAt, cancellationToken);
        // Rotation must not renew the age of the original two-factor sign-in.
        if (startedAt < clock.GetUtcNow().AddMinutes(-5) || startedAt > clock.GetUtcNow()) return null;
        var pending = await PendingAsync(user!);
        if (pending is null || pending.FamilyId != familyId || pending.ExpiresAt <= clock.GetUtcNow())
        {
            pending = new PendingReplacement(Guid.NewGuid(), familyId, users.GenerateNewAuthenticatorKey(), clock.GetUtcNow().AddMinutes(10));
            if (!(await users.SetAuthenticationTokenAsync(user!, "LexiGo", PendingName, JsonSerializer.Serialize(pending))).Succeeded)
                throw new InvalidOperationException("Unable to stage authenticator replacement.");
        }
        await transaction.CommitAsync(cancellationToken);
        return new(pending.Id, pending.Key, pending.ExpiresAt);
    }

    public async Task<string[]?> ConfirmAsync(Guid userId, Guid familyId, Guid requestId, string password,
        string code, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAsync(userId, cancellationToken);
        if (!await PasswordMatchesAsync(user, password))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (!await SessionMatchesAsync(user!, familyId, cancellationToken)) return null;
        var pending = await PendingAsync(user!);
        if (pending is null || pending.Id != requestId || pending.FamilyId != familyId || pending.ExpiresAt <= clock.GetUtcNow()) return null;
        string oldKey = (await users.GetAuthenticatorKeyAsync(user!))!;
        await SetKeyAsync(user!, pending.Key);
        if (!await codes.VerifyAsync(user!, code))
        {
            // Preserve the old authenticator and commit only the failed-attempt counter.
            await SetKeyAsync(user!, oldKey);
            await users.AccessFailedAsync(user!);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        string[] recovery = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user!, 10))!.ToArray();
        if (!(await users.RemoveAuthenticationTokenAsync(user!, "LexiGo", PendingName)).Succeeded
            || !(await users.UpdateSecurityStampAsync(user!)).Succeeded)
            throw new InvalidOperationException("Unable to finalize authenticator replacement.");
        await sessions.RevokeAllAsync(userId, cancellationToken);
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = userId,
            Action = "AuthenticatorReplaced",
            EntityType = "User",
            EntityId = userId.ToString(),
            Reason = "Owner verified a replacement after a recent two-factor sign-in.",
            TraceId = Guid.NewGuid().ToString("N"),
            OccurredAt = clock.GetUtcNow()
        });
        database.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            UserId = userId,
            Recipient = user!.Email!,
            Purpose = "SecurityNotification",
            Subject = "Your LexiGo authenticator was replaced",
            HtmlBody = "<h1>Your authenticator was replaced.</h1><p>Two-factor authentication remains enabled. All devices were signed out and previous recovery codes are no longer valid. Contact support immediately if this was not you.</p>"
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return recovery;
    }

    private async Task<ApplicationUser?> LockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is not null) await database.Entry(user).ReloadAsync(cancellationToken);
        return user;
    }

    private async Task<bool> PasswordMatchesAsync(ApplicationUser? user, string password)
    {
        if (user is null || user.Status != "Active" || !user.EmailConfirmed || !user.TwoFactorEnabled || await users.IsLockedOutAsync(user)) return false;
        if (await users.CheckPasswordAsync(user, password)) return true;
        await users.AccessFailedAsync(user);
        return false;
    }

    private Task<bool> SessionMatchesAsync(ApplicationUser user, Guid familyId, CancellationToken cancellationToken) =>
        database.RefreshSessions.AnyAsync(item => item.UserId == user.Id && item.FamilyId == familyId && item.MfaVerified
            && item.SecurityStamp == user.SecurityStamp && item.RevokedAt == null && item.ExpiresAt > clock.GetUtcNow(), cancellationToken);

    private async Task<PendingReplacement?> PendingAsync(ApplicationUser user)
    {
        string? value = await users.GetAuthenticationTokenAsync(user, "LexiGo", PendingName);
        return value is null ? null : JsonSerializer.Deserialize<PendingReplacement>(value);
    }

    private async Task SetKeyAsync(ApplicationUser user, string key)
    {
        if (!(await users.SetAuthenticationTokenAsync(user, "[AspNetUserStore]", "AuthenticatorKey", key)).Succeeded)
            throw new InvalidOperationException("Unable to update authenticator key.");
    }

    private sealed record PendingReplacement(Guid Id, Guid FamilyId, string Key, DateTimeOffset ExpiresAt);
}

public sealed record ReplacementSetup(Guid RequestId, string SharedKey, DateTimeOffset ExpiresAt);
