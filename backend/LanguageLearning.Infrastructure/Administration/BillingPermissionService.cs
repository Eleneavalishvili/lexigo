using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed record BillingPermissionView(Guid UserId, bool Granted, bool Eligible, bool Inherited);
public sealed record BillingPermissionOutcome(BillingPermissionView? Value = null, bool Forbidden = false, bool Missing = false, string? Error = null);

public sealed class BillingPermissionService(LexiGoDbContext database, UserManager<ApplicationUser> users,
    BillingAdministrationAccess access, SessionService sessions, TimeProvider clock)
{
    public async Task<BillingPermissionOutcome> GetAsync(Guid actorId, Guid familyId, Guid targetId, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken, superOnly: true)) return new(Forbidden: true);
        var view = await ViewAsync(targetId, cancellationToken);
        return view is null ? new(Missing: true) : new(view);
    }

    public async Task<BillingPermissionOutcome> SetAsync(Guid actorId, Guid familyId, Guid targetId, bool enabled,
        string reason, string password, Guid key, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken, superOnly: true)) return new(Forbidden: true);
        if (key == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 3 or > 500 || reason.Any(char.IsControl))
            return new(Error: "Use a valid request key and a reason of 3–500 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AdministrationLocks.AcquireAsync(database, [actorId, targetId], cancellationToken);
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken, superOnly: true)) return new(Forbidden: true);
        if (!await access.RecentProofAsync(actorId, familyId, password, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(Error: "Use your current password and a two-factor sign-in from the last five minutes.");
        }
        string request = JsonSerializer.Serialize(new { targetId, enabled, reason = reason.Trim() });
        var previous = await database.BillingOperations.SingleOrDefaultAsync(item => item.UserId == actorId && item.Operation == "Billing.Permission" && item.IdempotencyKey == key, cancellationToken);
        if (previous is not null) return JsonNode.DeepEquals(JsonNode.Parse(previous.RequestSnapshot), JsonNode.Parse(request))
            ? new(JsonSerializer.Deserialize<BillingPermissionView>(previous.ResponseSnapshot)!) : new(Error: "This permission key belongs to different details.");
        var target = await users.FindByIdAsync(targetId.ToString());
        if (target is null) return new(Missing: true);
        var before = (await ViewAsync(targetId, cancellationToken))!;
        if (before.Inherited) return new(Error: "Super Admin access is inherited from its role; change that role through account administration.");
        if (enabled && !before.Eligible) return new(Error: "The recipient must be an active, confirmed Admin with two-factor enrollment.");
        var claims = (await users.GetClaimsAsync(target)).Where(item => item.Type == BillingPermissions.ClaimType && item.Value == BillingPermissions.Manage).ToArray();
        bool changed = enabled != claims.Any();
        if (changed)
        {
            var result = enabled ? await users.AddClaimAsync(target, new Claim(BillingPermissions.ClaimType, BillingPermissions.Manage))
                : await users.RemoveClaimsAsync(target, claims);
            if (!result.Succeeded || !(await users.UpdateSecurityStampAsync(target)).Succeeded) throw new InvalidOperationException("Unable to update billing delegation.");
            await sessions.RevokeAllAsync(targetId, cancellationToken);
        }
        var response = (await ViewAsync(targetId, cancellationToken))!;
        var now = clock.GetUtcNow();
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = enabled ? "Billing.Permission.Granted" : "Billing.Permission.Revoked",
            EntityType = "BillingPermission",
            EntityId = targetId.ToString(),
            Reason = reason.Trim(),
            TraceId = key.ToString(),
            OccurredAt = now
        });
        database.BillingOperations.Add(new BillingOperation
        {
            UserId = actorId,
            Operation = "Billing.Permission",
            IdempotencyKey = key,
            RequestSnapshot = request,
            ResponseSnapshot = JsonSerializer.Serialize(response),
            CreatedAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(response);
    }

    private async Task<BillingPermissionView?> ViewAsync(Guid targetId, CancellationToken cancellationToken)
    {
        var target = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (target is null) return null;
        var roles = await (from link in database.UserRoles
                           join role in database.Roles on link.RoleId equals role.Id
                           where link.UserId == targetId
                           select role.Name).ToArrayAsync(cancellationToken);
        bool inherited = roles.Contains(RoleNames.SuperAdmin);
        bool eligible = roles.Contains(RoleNames.Admin) && target.Status == "Active" && target.EmailConfirmed && target.TwoFactorEnabled && !inherited;
        bool granted = inherited || roles.Contains(RoleNames.Admin) && await database.UserClaims.AnyAsync(item => item.UserId == targetId
            && item.ClaimType == BillingPermissions.ClaimType && item.ClaimValue == BillingPermissions.Manage, cancellationToken);
        return new(targetId, granted, eligible, inherited);
    }
}
