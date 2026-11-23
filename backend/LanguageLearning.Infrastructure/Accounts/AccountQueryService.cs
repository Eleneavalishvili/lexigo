using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class AccountQueryService(LexiGoDbContext database, UserManager<ApplicationUser> users, TimeProvider clock)
{
    public async Task<AccountSummary?> CurrentAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return null;
        return await SummaryAsync(user, (await users.GetRolesAsync(user)).ToArray());
    }

    public async Task<AccountSummary> SummaryAsync(ApplicationUser user, IReadOnlyList<string> roles)
    {
        bool billing = roles.Contains(RoleNames.SuperAdmin) || roles.Contains(RoleNames.Admin)
            && await database.UserClaims.AnyAsync(item => item.UserId == user.Id && item.ClaimType == BillingPermissions.ClaimType && item.ClaimValue == BillingPermissions.Manage);
        return new(user.Id, user.Email!, user.DisplayName, user.EmailConfirmed, roles, billing ? [BillingPermissions.Manage] : []);
    }

    public async Task<IReadOnlyList<ActiveSessionSummary>> SessionsAsync(Guid userId, Guid currentFamily, CancellationToken cancellationToken) =>
        await database.RefreshSessions.AsNoTracking().Where(session => session.UserId == userId && session.RevokedAt == null && session.ExpiresAt > clock.GetUtcNow())
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new ActiveSessionSummary(session.FamilyId, session.DeviceLabel,
                database.RefreshSessions.Where(previous => previous.FamilyId == session.FamilyId).Min(previous => previous.CreatedAt),
                session.ExpiresAt, session.FamilyId == currentFamily))
            .ToArrayAsync(cancellationToken);
}

public sealed record ActiveSessionSummary(Guid Id, string DeviceLabel, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool IsCurrent);
