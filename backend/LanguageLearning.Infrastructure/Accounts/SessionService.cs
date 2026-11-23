using System.Security.Cryptography;
using System.Text;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class SessionService(LexiGoDbContext database, UserManager<ApplicationUser> users, TimeProvider clock)
{
    public async Task<(RefreshSession Session, string RawToken)> CreateAsync(ApplicationUser user, string device, bool mfa, CancellationToken cancellationToken)
    {
        string rawToken = NewToken();
        var session = new RefreshSession
        {
            UserId = user.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = Hash(rawToken),
            SecurityStamp = user.SecurityStamp!,
            DeviceLabel = CleanDevice(device),
            MfaVerified = mfa,
            CreatedAt = clock.GetUtcNow(),
            ExpiresAt = clock.GetUtcNow().AddDays(7)
        };
        // Login holds the user row lock, so concurrent sign-ins cannot bypass this cap.
        var excessFamilies = await database.RefreshSessions.Where(item => item.UserId == user.Id && item.RevokedAt == null && item.ExpiresAt > clock.GetUtcNow())
            .OrderByDescending(item => item.CreatedAt).Skip(9).Select(item => item.FamilyId).ToArrayAsync(cancellationToken);
        foreach (Guid family in excessFamilies) await RevokeFamilyAsync(family, "SessionLimit", cancellationToken);
        database.RefreshSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        return (session, rawToken);
    }

    public async Task<(RefreshSession Session, string RawToken)?> RotateAsync(string? token, CancellationToken cancellationToken)
    {
        if (token is null || token.Length != 64) return null;
        string hash = Hash(token);
        var owner = await database.RefreshSessions.AsNoTracking().Where(item => item.TokenHash == hash).Select(item => (Guid?)item.UserId).SingleOrDefaultAsync(cancellationToken);
        if (owner is null) return null;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(owner.Value, cancellationToken);
        // The lock serializes two attempts to redeem the same opaque token.
        var matches = await database.RefreshSessions.FromSqlInterpolated(
            $"SELECT * FROM \"RefreshSessions\" WHERE \"TokenHash\" = {hash} FOR UPDATE").ToListAsync(cancellationToken);
        var previous = matches.SingleOrDefault();
        if (previous is null) return null;
        if (previous.RevokedAt is not null)
        {
            if (previous.ReplacedById is not null) await RevokeFamilyAsync(previous.FamilyId, "TokenReuse", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var roles = user is null ? [] : await users.GetRolesAsync(user);
        if (user is null || !user.EmailConfirmed || user.Status != "Active" || previous.ExpiresAt <= clock.GetUtcNow()
            || previous.SecurityStamp != user.SecurityStamp || (roles.Any(role => role != RoleNames.Learner) && !previous.MfaVerified))
        {
            await RevokeFamilyAsync(previous.FamilyId, "AccountChanged", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        previous.RevokedAt = clock.GetUtcNow();
        previous.RevocationReason = "Rotated";
        await database.SaveChangesAsync(cancellationToken);
        string rawToken = NewToken();
        var replacement = new RefreshSession
        {
            UserId = previous.UserId,
            FamilyId = previous.FamilyId,
            TokenHash = Hash(rawToken),
            SecurityStamp = previous.SecurityStamp,
            DeviceLabel = previous.DeviceLabel,
            MfaVerified = previous.MfaVerified,
            CreatedAt = clock.GetUtcNow(),
            ExpiresAt = previous.ExpiresAt
        };
        database.RefreshSessions.Add(replacement);
        await database.SaveChangesAsync(cancellationToken);
        previous.ReplacedById = replacement.Id;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (replacement, rawToken);
    }

    public async Task SignOutAsync(string? token, CancellationToken cancellationToken)
    {
        if (token is null || token.Length != 64) return;
        string hash = Hash(token);
        var session = await database.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (session is null) return;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(session.UserId, cancellationToken);
        await RevokeFamilyAsync(session.FamilyId, "SignOut", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task RevokeFamilyAsync(Guid family, string reason, CancellationToken cancellationToken) => database.RefreshSessions
        .Where(session => session.FamilyId == family && session.RevokedAt == null)
        .ExecuteUpdateAsync(updates => updates.SetProperty(session => session.RevokedAt, clock.GetUtcNow())
            .SetProperty(session => session.RevocationReason, reason), cancellationToken);

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
        if (transaction is not null) await LockUserAsync(userId, cancellationToken);
        await database.RefreshSessions.Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(session => session.RevokedAt, clock.GetUtcNow())
                .SetProperty(session => session.RevocationReason, "AccountSignOut"), cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> RevokeOwnedAsync(Guid userId, Guid familyId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId, cancellationToken);
        if (!await database.RefreshSessions.AnyAsync(session => session.UserId == userId && session.FamilyId == familyId, cancellationToken)) return false;
        await RevokeFamilyAsync(familyId, "UserRevoked", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<ApplicationUser?> LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var matches = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").ToListAsync(cancellationToken);
        var user = matches.SingleOrDefault();
        if (user is not null) await database.Entry(user).ReloadAsync(cancellationToken);
        return user;
    }

    private static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string CleanDevice(string device)
    {
        string browser = device switch
        {
            var text when text.Contains("Edg/", StringComparison.OrdinalIgnoreCase) => "Microsoft Edge",
            var text when text.Contains("OPR/", StringComparison.OrdinalIgnoreCase) => "Opera",
            var text when text.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) => "Firefox",
            var text when text.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) => "Chrome",
            var text when text.Contains("Safari/", StringComparison.OrdinalIgnoreCase) => "Safari",
            _ => "Web browser"
        };
        string platform = device switch
        {
            var text when text.Contains("Windows", StringComparison.OrdinalIgnoreCase) => "Windows",
            var text when text.Contains("Android", StringComparison.OrdinalIgnoreCase) => "Android",
            var text when text.Contains("iPhone", StringComparison.OrdinalIgnoreCase) => "iPhone",
            var text when text.Contains("iPad", StringComparison.OrdinalIgnoreCase) => "iPad",
            var text when text.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) => "Mac",
            var text when text.Contains("Linux", StringComparison.OrdinalIgnoreCase) => "Linux",
            _ => "this device"
        };
        return $"{browser} on {platform}";
    }
}
