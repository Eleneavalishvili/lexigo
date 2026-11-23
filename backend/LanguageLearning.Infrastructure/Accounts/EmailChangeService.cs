using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class EmailChangeService(LexiGoDbContext database, UserManager<ApplicationUser> users,
    SessionService sessions, MfaCodeVerifier codes, IOptions<AccountEmailOptions> options, TimeProvider clock)
{
    private const string PendingName = "PendingEmailChange";

    public async Task<bool> RequestAsync(Guid userId, string email, string password, string? code,
        string? recoveryCode, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAsync(userId, cancellationToken);
        if (user is null || user.Status != "Active" || !user.EmailConfirmed || await users.IsLockedOutAsync(user)) return false;
        bool proof = await users.CheckPasswordAsync(user, password);
        if (proof && user.TwoFactorEnabled)
            proof = !string.IsNullOrWhiteSpace(recoveryCode)
                ? (await users.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode.Trim())).Succeeded
                : !string.IsNullOrWhiteSpace(code) && await codes.VerifyAsync(user, code);
        if (!proof)
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        // Return the same accepted response for occupied addresses and cooldowns.
        string target = email.Trim();
        bool recent = await database.EmailOutboxMessages.AnyAsync(message => message.UserId == userId
            && message.Purpose == "EmailChange" && message.CreatedAt > clock.GetUtcNow().AddMinutes(-5), cancellationToken);
        if (recent || await users.FindByEmailAsync(target) is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        var pending = new PendingEmailChange(Guid.NewGuid(), target, clock.GetUtcNow());
        string token = await users.GenerateChangeEmailTokenAsync(user, target);
        string encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var result = await users.SetAuthenticationTokenAsync(user, "LexiGo", PendingName, JsonSerializer.Serialize(pending));
        if (!result.Succeeded) throw new InvalidOperationException("Unable to save email change request.");
        string url = $"{options.Value.WebOrigin.TrimEnd('/')}/confirm-email-change?userId={user.Id}&requestId={pending.Id}&token={encoded}";
        database.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            UserId = user.Id,
            Recipient = target,
            Purpose = "EmailChange",
            Subject = "Confirm your new LexiGo email",
            HtmlBody = $"<h1>A new place for your little letters.</h1><p>Confirm your new email address.</p><p><a href=\"{HtmlEncoder.Default.Encode(url)}\">Confirm my new email</a></p><p>This link expires in 30 minutes. Your current email remains unchanged until confirmation. Ignore this if you did not request it.</p>"
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ConfirmAsync(Guid userId, Guid requestId, string encodedToken, CancellationToken cancellationToken)
    {
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken)); }
        catch (FormatException) { return false; }
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await LockAsync(userId, cancellationToken);
            if (user is null || user.Status != "Active" || !user.EmailConfirmed) return false;
            string? stored = await users.GetAuthenticationTokenAsync(user, "LexiGo", PendingName);
            if (stored is null) return false;
            var pending = JsonSerializer.Deserialize<PendingEmailChange>(stored);
            if (pending is null || pending.Id != requestId || pending.CreatedAt.AddMinutes(30) <= clock.GetUtcNow()) return false;
            var occupied = await users.FindByEmailAsync(pending.Email);
            if (occupied is not null) return false;
            string oldEmail = user.Email!;
            if (!(await users.ChangeEmailAsync(user, pending.Email, token)).Succeeded) return false;
            if (!(await users.SetUserNameAsync(user, pending.Email)).Succeeded) return false;
            if (!(await users.RemoveAuthenticationTokenAsync(user, "LexiGo", PendingName)).Succeeded)
                throw new InvalidOperationException("Unable to consume email change request.");
            await sessions.RevokeAllAsync(user.Id, cancellationToken);
            foreach (string recipient in new[] { oldEmail, pending.Email })
                database.EmailOutboxMessages.Add(new EmailOutboxMessage
                {
                    UserId = user.Id,
                    Recipient = recipient,
                    Purpose = "SecurityNotification",
                    Subject = "Your LexiGo email address changed",
                    HtmlBody = "<h1>Your account email changed.</h1><p>All existing sessions have been signed out. If this was not you, contact LexiGo support immediately.</p>"
                });
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    private async Task<ApplicationUser?> LockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is not null) await database.Entry(user).ReloadAsync(cancellationToken);
        return user;
    }

    private sealed record PendingEmailChange(Guid Id, string Email, DateTimeOffset CreatedAt);
}
