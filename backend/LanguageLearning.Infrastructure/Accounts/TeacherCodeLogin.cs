using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed partial class LoginService
{
    public async Task<LoginResult> SignInTeacherAsync(string accessCode, string authenticatorCode,
        string device, CancellationToken token)
    {
        string? hash = teacherAccess.Hash(accessCode);
        var id = hash is null ? null : await database.Users.AsNoTracking()
            .Where(user => user.TeacherAccessCodeHash == hash).Select(user => (Guid?)user.Id).SingleOrDefaultAsync(token);
        if (id is null)
        {
            _ = users.PasswordHasher.HashPassword(new ApplicationUser { DisplayName = "Unknown" }, accessCode);
            return new(LoginStatus.InvalidCredentials);
        }
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {id.Value} FOR UPDATE")
            .SingleAsync(token);
        await database.Entry(user).ReloadAsync(token);
        if (user.TeacherAccessCodeHash != hash || user.Status != "Active" || !user.EmailConfirmed
            || !user.TwoFactorEnabled || await users.IsLockedOutAsync(user) || !await users.IsInRoleAsync(user, RoleNames.Teacher))
            return new(LoginStatus.InvalidCredentials);
        if (!await codes.VerifyAsync(user, authenticatorCode))
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync(token);
            return new(LoginStatus.InvalidCredentials);
        }
        await users.ResetAccessFailedCountAsync(user);
        var roles = await users.GetRolesAsync(user);
        var issued = await sessions.CreateAsync(user, device, true, token);
        var ticket = await TicketAsync(user, roles, issued.Session, issued.RawToken);
        await transaction.CommitAsync(token);
        return new(LoginStatus.Success, ticket);
    }
}
