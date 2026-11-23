using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed class UserAdministrationService(LexiGoDbContext database, UserManager<ApplicationUser> users, SessionService sessions, TimeProvider clock)
{
    public async Task<AdministrationResult> UpdateAsync(Guid actorId, Guid familyId, Guid targetId, string password,
        string reason, string traceId, string? status, string[]? roles, int[]? languageIds, CancellationToken cancellationToken)
    {
        bool roleChange = roles is not null;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 3 or > 500 || reason.Any(char.IsControl)
            || (roleChange && (roles!.Length is < 1 or > 7 || roles.Distinct().Count() != roles.Length || !roles.Contains(RoleNames.Learner) || roles.Except(RoleNames.All).Any()
                || languageIds is null || languageIds.Length > 4 || languageIds.Distinct().Count() != languageIds.Length))
            || (!roleChange && status is not ("Active" or "Blocked"))) return AdministrationResult.Invalid;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AdministrationLocks.AcquireAsync(database, [actorId, targetId], cancellationToken);
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null) return AdministrationResult.Forbidden;
        await database.Entry(actor).ReloadAsync(cancellationToken);
        var actorRoles = await users.GetRolesAsync(actor);
        bool superAdmin = actorRoles.Contains(RoleNames.SuperAdmin);
        if (actor.Status != "Active" || !actor.EmailConfirmed || !actor.TwoFactorEnabled || (roleChange ? !superAdmin : !superAdmin && !actorRoles.Contains(RoleNames.Admin))) return AdministrationResult.Forbidden;
        bool activeSession = await database.RefreshSessions.AnyAsync(item => item.UserId == actorId && item.FamilyId == familyId && item.MfaVerified
            && item.RevokedAt == null && item.ExpiresAt > clock.GetUtcNow() && item.SecurityStamp == actor.SecurityStamp, cancellationToken);
        if (!activeSession) return AdministrationResult.Forbidden;
        var started = await database.RefreshSessions.Where(item => item.UserId == actorId && item.FamilyId == familyId).MinAsync(item => item.CreatedAt, cancellationToken);
        if (started < clock.GetUtcNow().AddMinutes(-5) || started > clock.GetUtcNow() || await users.IsLockedOutAsync(actor)) return AdministrationResult.Invalid;
        if (!await users.CheckPasswordAsync(actor, password))
        {
            await users.AccessFailedAsync(actor);
            await transaction.CommitAsync(cancellationToken);
            return AdministrationResult.Invalid;
        }
        var target = await users.FindByIdAsync(targetId.ToString());
        if (target is null) return AdministrationResult.NotFound;
        await database.Entry(target).ReloadAsync(cancellationToken);
        var oldRoles = await users.GetRolesAsync(target);
        if (!superAdmin && (oldRoles.Contains(RoleNames.Admin) || oldRoles.Contains(RoleNames.SuperAdmin))) return AdministrationResult.Forbidden;
        bool removingActiveAdmin = target.Status == "Active" && oldRoles.Contains(RoleNames.SuperAdmin)
            && (roleChange ? !roles!.Contains(RoleNames.SuperAdmin) : status != "Active");
        if (removingActiveAdmin)
        {
            int remaining = await (from link in database.UserRoles
                                   join role in database.Roles on link.RoleId equals role.Id
                                   join user in database.Users on link.UserId equals user.Id
                                   where role.Name == RoleNames.SuperAdmin && user.Status == "Active" && user.EmailConfirmed && user.TwoFactorEnabled
                                   select user.Id).CountAsync(cancellationToken);
            if (remaining <= 1) return AdministrationResult.LastAdministrator;
        }
        string auditReason;
        if (roles is not null)
        {
            if (roles!.Any(role => role != RoleNames.Learner) && (!target.EmailConfirmed || !target.TwoFactorEnabled || target.Status != "Active")) return AdministrationResult.EnrollmentRequired;
            int validLanguages = await database.Languages.CountAsync(item => languageIds!.Contains(item.Id) && (item.Code == "es" || item.Code == "it" || item.Code == "de" || item.Code == "ru"), cancellationToken);
            if (validLanguages != languageIds!.Length) return AdministrationResult.Invalid;
            var oldScopes = await database.UserLanguageScopes.Where(item => item.UserId == targetId).ToArrayAsync(cancellationToken);
            if (oldRoles.Order().SequenceEqual(roles.Order()) && oldScopes.Select(item => item.LanguageId).Order().SequenceEqual(languageIds.Order())) return AdministrationResult.Success;
            if (!(await users.RemoveFromRolesAsync(target, oldRoles.Except(roles))).Succeeded || !(await users.AddToRolesAsync(target, roles.Except(oldRoles))).Succeeded)
                throw new InvalidOperationException("Unable to update account roles.");
            if (!roles.Contains(RoleNames.Admin))
            {
                var billingClaims = (await users.GetClaimsAsync(target)).Where(item => item.Type == BillingPermissions.ClaimType && item.Value == BillingPermissions.Manage);
                if (!(await users.RemoveClaimsAsync(target, billingClaims)).Succeeded) throw new InvalidOperationException("Unable to clear removed billing delegation.");
            }
            database.UserLanguageScopes.RemoveRange(oldScopes);
            await database.SaveChangesAsync(cancellationToken);
            database.UserLanguageScopes.AddRange(languageIds.Select(id => new UserLanguageScope { UserId = targetId, LanguageId = id }));
            auditReason = $"{reason.Trim()}\nRoles: {string.Join(',', oldRoles.Order())} -> {string.Join(',', roles.Order())}; language IDs: {string.Join(',', oldScopes.Select(item => item.LanguageId).Order())} -> {string.Join(',', languageIds.Order())}";
        }
        else
        {
            if (target.Status == status) return AdministrationResult.Success;
            if (target.Status is not ("Active" or "Blocked")) return AdministrationResult.Invalid;
            auditReason = $"{reason.Trim()}\nStatus: {target.Status} -> {status}";
            target.Status = status!;
        }
        if (!(await users.UpdateSecurityStampAsync(target)).Succeeded) throw new InvalidOperationException("Unable to invalidate changed account access.");
        await sessions.RevokeAllAsync(targetId, cancellationToken);
        database.AuditLogs.Add(new AuditLog { ActorId = actorId, Action = roleChange ? "UserRolesChanged" : "UserStatusChanged", EntityType = "User", EntityId = targetId.ToString(), Reason = auditReason, TraceId = traceId, OccurredAt = clock.GetUtcNow() });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdministrationResult.Success;
    }
}

public enum AdministrationResult { Success, Invalid, Forbidden, NotFound, LastAdministrator, EnrollmentRequired }
