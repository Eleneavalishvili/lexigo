using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class ContentAccess(LexiGoDbContext database, UserManager<ApplicationUser> users, TimeProvider clock)
{
    public async Task<ContentActor?> ActorAsync(Guid userId, Guid familyId, bool forUpdate,
        CancellationToken cancellationToken, bool allowModerator = false)
    {
        if (forUpdate)
            _ = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").ToArrayAsync(cancellationToken);
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return null;
        if (forUpdate) await database.Entry(user).ReloadAsync(cancellationToken);
        if (user.Status != "Active" || !user.EmailConfirmed || !user.TwoFactorEnabled) return null;
        if (!await database.RefreshSessions.AnyAsync(item => item.UserId == userId && item.FamilyId == familyId && item.RevokedAt == null
            && item.ExpiresAt > clock.GetUtcNow() && item.SecurityStamp == user.SecurityStamp && item.MfaVerified, cancellationToken)) return null;
        string[] roles = (await users.GetRolesAsync(user)).ToArray();
        if (!roles.Any(role => role is RoleNames.ContentAuthor or RoleNames.Reviewer or RoleNames.Admin or RoleNames.SuperAdmin)
            && !(allowModerator && roles.Contains(RoleNames.Moderator))) return null;
        int[] scopes = await database.UserLanguageScopes.Where(item => item.UserId == userId).Select(item => item.LanguageId).ToArrayAsync(cancellationToken);
        return new(userId, roles, scopes);
    }
}

public sealed record ContentActor(Guid Id, string[] Roles, int[] LanguageIds)
{
    public bool IsAdmin => Roles.Contains(RoleNames.Admin) || Roles.Contains(RoleNames.SuperAdmin);
    public bool CanAuthor(int languageId) => IsAdmin || Roles.Contains(RoleNames.ContentAuthor) && LanguageIds.Contains(languageId);
    public bool CanEdit(int languageId, Guid ownerId) => IsAdmin || Id == ownerId && CanAuthor(languageId);
    public bool CanRead(int languageId, Guid ownerId, string status) => CanEdit(languageId, ownerId)
        || Roles.Contains(RoleNames.Reviewer) && LanguageIds.Contains(languageId) && status is "Submitted" or "Approved";
    public bool CanReview(int languageId, Guid ownerId, Guid createdBy) => Roles.Contains(RoleNames.Reviewer)
        && LanguageIds.Contains(languageId) && Id != ownerId && Id != createdBy;
}
