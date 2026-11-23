using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Api.Authentication;

public sealed class SessionValidationEvents(LexiGoDbContext database, UserManager<ApplicationUser> users, TimeProvider clock) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId)
            || !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var familyId))
        {
            context.Fail("Invalid session.");
            return;
        }
        var session = await database.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.UserId == userId && item.FamilyId == familyId && item.RevokedAt == null && item.ExpiresAt > clock.GetUtcNow(), context.HttpContext.RequestAborted);
        var user = await users.FindByIdAsync(userId.ToString());
        if (session is null || user is null || !user.EmailConfirmed || user.Status != "Active" || session.SecurityStamp != user.SecurityStamp)
        {
            context.Fail("Session no longer active.");
            return;
        }
        var roles = await users.GetRolesAsync(user);
        var claimedRoles = context.Principal!.FindAll("role").Select(claim => claim.Value).Order().ToArray();
        bool elevated = roles.Any(role => role != RoleNames.Learner);
        if (!roles.Order().SequenceEqual(claimedRoles) || (elevated && (!session.MfaVerified || !context.Principal.HasClaim("amr", "mfa"))))
            context.Fail("Account permissions changed.");
    }
}
