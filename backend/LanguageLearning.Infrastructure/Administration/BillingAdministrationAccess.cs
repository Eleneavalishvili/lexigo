using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed class BillingAdministrationAccess(LexiGoDbContext database, UserManager<ApplicationUser> users, TimeProvider clock)
{
    public async Task<bool> AllowedAsync(Guid actorId, Guid familyId, CancellationToken cancellationToken, bool superOnly = false)
    {
        var now = clock.GetUtcNow();
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == actorId, cancellationToken);
        if (actor is null || actor.Status != "Active" || !actor.EmailConfirmed || !actor.TwoFactorEnabled || actor.LockoutEnd > now) return false;
        bool session = await database.RefreshSessions.AnyAsync(item => item.UserId == actorId && item.FamilyId == familyId
            && item.MfaVerified && item.RevokedAt == null && item.ExpiresAt > now && item.SecurityStamp == actor.SecurityStamp, cancellationToken);
        if (!session) return false;
        var roles = await (from link in database.UserRoles
                           join role in database.Roles on link.RoleId equals role.Id
                           where link.UserId == actorId
                           select role.Name).ToArrayAsync(cancellationToken);
        return roles.Contains(RoleNames.SuperAdmin) || !superOnly && roles.Contains(RoleNames.Admin)
            && await database.UserClaims.AnyAsync(item => item.UserId == actorId && item.ClaimType == BillingPermissions.ClaimType
                && item.ClaimValue == BillingPermissions.Manage, cancellationToken);
    }

    public async Task<bool> RecentProofAsync(Guid actorId, Guid familyId, string password, CancellationToken cancellationToken)
    {
        var started = await database.RefreshSessions.Where(item => item.UserId == actorId && item.FamilyId == familyId)
            .Select(item => (DateTimeOffset?)item.CreatedAt).MinAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (started is null || started < now.AddMinutes(-5) || started > now || password.Length is < 1 or > 128) return false;
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null || await users.IsLockedOutAsync(actor)) return false;
        if (await users.CheckPasswordAsync(actor, password)) return true;
        await users.AccessFailedAsync(actor);
        return false;
    }
}
