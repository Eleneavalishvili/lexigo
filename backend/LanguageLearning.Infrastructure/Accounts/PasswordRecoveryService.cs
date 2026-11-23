using System.Text;
using System.Text.Encodings.Web;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class PasswordRecoveryService(
    LexiGoDbContext database, UserManager<ApplicationUser> users, SessionService sessions,
    IOptions<AccountEmailOptions> options, TimeProvider clock)
{
    public async Task RequestResetAsync(string email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || user.Status != "Active") return;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        _ = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {user.Id} FOR UPDATE").ToListAsync(cancellationToken);
        await database.Entry(user).ReloadAsync(cancellationToken);
        if (user.Status != "Active") return;
        bool recent = await database.EmailOutboxMessages.AnyAsync(message => message.UserId == user.Id
            && message.Purpose == "PasswordReset" && message.CreatedAt > clock.GetUtcNow().AddMinutes(-5), cancellationToken);
        if (!recent)
        {
            string token = await users.GeneratePasswordResetTokenAsync(user);
            string encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            string url = $"{options.Value.WebOrigin.TrimEnd('/')}/reset-password?userId={user.Id}&token={encoded}";
            database.EmailOutboxMessages.Add(new EmailOutboxMessage
            {
                UserId = user.Id,
                Recipient = user.Email!,
                Purpose = "PasswordReset",
                Subject = "A fresh start for your LexiGo password",
                HtmlBody = $"<h1>A little fresh start.</h1><p>Use this link to choose a new password.</p><p><a href=\"{HtmlEncoder.Default.Encode(url)}\">Reset my password</a></p><p>This link expires in 30 minutes. If you did not request it, ignore this email. Your password has not changed.</p>"
            });
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> ResetAsync(Guid userId, string encodedToken, string password, CancellationToken cancellationToken)
    {
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken)); }
        catch (FormatException) { return false; }
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var matches = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").ToListAsync(cancellationToken);
        var user = matches.SingleOrDefault();
        if (user is null || user.Status != "Active") return false;
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded) return false;
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        QueueChangedNotice(user);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ChangeAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var matches = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").ToListAsync(cancellationToken);
        var user = matches.SingleOrDefault();
        if (user is null || user.Status != "Active") return false;
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded) return false;
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        QueueChangedNotice(user);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private void QueueChangedNotice(ApplicationUser user) => database.EmailOutboxMessages.Add(new EmailOutboxMessage
    {
        UserId = user.Id,
        Recipient = user.Email!,
        Purpose = "SecurityNotification",
        Subject = "Your LexiGo password was changed",
        HtmlBody = "<h1>Your password was changed.</h1><p>All existing sessions have been signed out. If this was not you, request a password reset from the LexiGo sign-in page.</p>"
    });
}
