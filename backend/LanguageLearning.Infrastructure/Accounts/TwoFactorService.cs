using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class TwoFactorService(LexiGoDbContext database, UserManager<ApplicationUser> users, SessionService sessions, MfaCodeVerifier codes)
{
    public async Task<object?> StatusAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : new { enabled = user.TwoFactorEnabled, recoveryCodesRemaining = await users.CountRecoveryCodesAsync(user) };
    }

    public async Task<AuthenticatorSetup?> BeginAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAsync(userId, cancellationToken);
        if (!await PasswordMatchesAsync(user, password) || user!.TwoFactorEnabled)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        // An existing pending key is reused; starting setup again never resets another active setup.
        string? key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            key = users.GenerateNewAuthenticatorKey();
            // A pending, disabled key does not change the security stamp or invalidate setup access.
            if (!(await users.SetAuthenticationTokenAsync(user, "[AspNetUserStore]", "AuthenticatorKey", key)).Succeeded)
                throw new InvalidOperationException("Unable to initialize authenticator.");
        }
        string label = Uri.EscapeDataString("LexiGo:" + user.Email);
        string uri = $"otpauth://totp/{label}?secret={key}&issuer=LexiGo&digits=6&period=30";
        await transaction.CommitAsync(cancellationToken);
        return new(key!, uri);
    }

    public async Task<string[]?> EnableAsync(Guid userId, string password, string code, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAsync(userId, cancellationToken);
        if (!await PasswordMatchesAsync(user, password) || user!.TwoFactorEnabled)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (!await codes.VerifyAsync(user, code))
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (!(await users.SetTwoFactorEnabledAsync(user, true)).Succeeded) throw new InvalidOperationException("Unable to enable two-factor authentication.");
        string[] recovery = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray()
            ?? throw new InvalidOperationException("Unable to generate recovery codes.");
        await InvalidateAsync(user, "Two-factor authentication was enabled", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return recovery;
    }

    public async Task<string[]?> RegenerateAsync(Guid userId, string password, string code, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAsync(userId, cancellationToken);
        if (!await PasswordMatchesAsync(user, password) || !user!.TwoFactorEnabled)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (!await codes.VerifyAsync(user, code))
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        string[] recovery = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray()
            ?? throw new InvalidOperationException("Unable to generate recovery codes.");
        await InvalidateAsync(user, "Your two-factor recovery codes were replaced", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return recovery;
    }

    public async Task<bool> DisableAsync(Guid userId, string password, string? code, string? recoveryCode, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAsync(userId, cancellationToken);
        if (!await PasswordMatchesAsync(user, password) || !user!.TwoFactorEnabled)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        if ((await users.GetRolesAsync(user)).Any(role => role != RoleNames.Learner)) return false;
        bool valid = !string.IsNullOrWhiteSpace(recoveryCode)
            ? (await users.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode.Trim())).Succeeded
            : !string.IsNullOrWhiteSpace(code) && await codes.VerifyAsync(user, code);
        if (!valid)
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        if (!(await users.SetTwoFactorEnabledAsync(user, false)).Succeeded) throw new InvalidOperationException("Unable to disable two-factor authentication.");
        await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
        await users.ResetAuthenticatorKeyAsync(user);
        await InvalidateAsync(user, "Two-factor authentication was disabled", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<ApplicationUser?> LockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var matches = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").ToListAsync(cancellationToken);
        var user = matches.SingleOrDefault();
        if (user is not null) await database.Entry(user).ReloadAsync(cancellationToken);
        return user;
    }

    private async Task<bool> PasswordMatchesAsync(ApplicationUser? user, string password)
    {
        if (user is null || user.Status != "Active" || !user.EmailConfirmed || await users.IsLockedOutAsync(user)) return false;
        if (await users.CheckPasswordAsync(user, password)) return true;
        await users.AccessFailedAsync(user);
        return false;
    }

    private async Task InvalidateAsync(ApplicationUser user, string subject, CancellationToken cancellationToken)
    {
        if (!(await users.UpdateSecurityStampAsync(user)).Succeeded) throw new InvalidOperationException("Unable to update account security.");
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        database.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            UserId = user.Id,
            Recipient = user.Email!,
            Purpose = "SecurityNotification",
            Subject = subject,
            HtmlBody = "<h1>Your LexiGo security settings changed.</h1><p>All devices have been signed out. If this was not you, reset your password and contact support.</p>"
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}

public sealed record AuthenticatorSetup(string SharedKey, string AuthenticatorUri);
