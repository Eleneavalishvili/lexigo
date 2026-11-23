using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

// Called only by the operator-controlled process command, never exposed as an HTTP endpoint.
public sealed class SuperAdminBootstrapService(LexiGoDbContext database, UserManager<ApplicationUser> users,
    SessionService sessions, MfaCodeVerifier codes, TimeProvider clock)
{
    public async Task<bool> PromoteAsync(string email, string password, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || password.Length is < 1 or > 128 || code.Length > 32) return false;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Every future administrator/role mutation must acquire this guard before user locks.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730027001)", cancellationToken);
        var role = await database.Roles.SingleOrDefaultAsync(item => item.NormalizedName == "SUPERADMIN", cancellationToken);
        if (role is null || await database.UserRoles.AnyAsync(item => item.RoleId == role.Id, cancellationToken)) return false;
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null) return false;
        _ = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {user.Id} FOR UPDATE").ToArrayAsync(cancellationToken);
        await database.Entry(user).ReloadAsync(cancellationToken);
        if (user.Status != "Active" || !user.EmailConfirmed || !user.TwoFactorEnabled || await users.IsLockedOutAsync(user)) return false;
        if (!await users.CheckPasswordAsync(user, password) || !await codes.VerifyAsync(user, code))
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        if (!(await users.AddToRoleAsync(user, RoleNames.SuperAdmin)).Succeeded
            || !(await users.UpdateSecurityStampAsync(user)).Succeeded)
            throw new InvalidOperationException("Unable to provision the initial administrator.");
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = user.Id,
            Action = "SuperAdminBootstrapped",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            Reason = "Initial operator-controlled provisioning after password and authenticator verification.",
            TraceId = Guid.NewGuid().ToString("N"),
            OccurredAt = clock.GetUtcNow()
        });
        database.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            UserId = user.Id,
            Recipient = user.Email!,
            Purpose = "SecurityNotification",
            Subject = "Your LexiGo administrator access was initialized",
            HtmlBody = "<h1>Administrator access initialized.</h1><p>All previous sessions were signed out. Two-factor authentication remains mandatory. Contact support immediately if this was not authorized.</p>"
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
