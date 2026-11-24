using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed partial class LoginService(LexiGoDbContext database, UserManager<ApplicationUser> users, SessionService sessions, IAccessTokenIssuer tokens, MfaCodeVerifier codes, AccountQueryService accounts, TeacherAccessHasher teacherAccess)
{
    public async Task<LoginResult> SignInAsync(string email, string password, string? code, string? recoveryCode, string device, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            _ = users.PasswordHasher.HashPassword(new ApplicationUser { DisplayName = "Unknown" }, password);
            return new(LoginStatus.InvalidCredentials);
        }
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        _ = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {user.Id} FOR UPDATE").ToListAsync(cancellationToken);
        await database.Entry(user).ReloadAsync(cancellationToken);
        bool correct = await users.CheckPasswordAsync(user, password);
        if (!user.EmailConfirmed || user.Status != "Active" || await users.IsLockedOutAsync(user)) return new(LoginStatus.InvalidCredentials);
        if (!correct)
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync(cancellationToken);
            return new(LoginStatus.InvalidCredentials);
        }
        var roles = await users.GetRolesAsync(user);
        if (roles.Any(role => role != RoleNames.Learner) && !user.TwoFactorEnabled) return new(LoginStatus.RequiresSecuritySetup);
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrEmpty(code) && string.IsNullOrWhiteSpace(recoveryCode)) return new(LoginStatus.RequiresTwoFactor);
            bool valid = !string.IsNullOrWhiteSpace(recoveryCode)
                ? (await users.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode.Trim())).Succeeded
                : await codes.VerifyAsync(user, code!);
            if (!valid)
            {
                await users.AccessFailedAsync(user);
                await transaction.CommitAsync(cancellationToken);
                return new(LoginStatus.InvalidCredentials);
            }
        }
        await users.ResetAccessFailedCountAsync(user);
        var issued = await sessions.CreateAsync(user, device, user.TwoFactorEnabled, cancellationToken);
        var ticket = await TicketAsync(user, roles, issued.Session, issued.RawToken);
        await transaction.CommitAsync(cancellationToken);
        return new(LoginStatus.Success, ticket);
    }

    public async Task<AuthenticationTicket?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        var rotated = await sessions.RotateAsync(refreshToken, cancellationToken);
        if (rotated is null) return null;
        var user = await users.FindByIdAsync(rotated.Value.Session.UserId.ToString());
        if (user is null) return null;
        var roles = await users.GetRolesAsync(user);
        return await TicketAsync(user, roles, rotated.Value.Session, rotated.Value.RawToken);
    }

    private async Task<AuthenticationTicket> TicketAsync(ApplicationUser user, IList<string> roles, RefreshSession session, string rawToken)
    {
        var account = await accounts.SummaryAsync(user, roles.ToArray());
        return new(tokens.Issue(user.Id, session.FamilyId, roles.ToArray(), session.MfaVerified), rawToken, session.ExpiresAt, account);
    }
}
