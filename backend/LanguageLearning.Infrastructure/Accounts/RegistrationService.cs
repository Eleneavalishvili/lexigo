using System.Text;
using System.Text.Encodings.Web;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class RegistrationService(
    LexiGoDbContext database,
    UserManager<ApplicationUser> users,
    IOptions<AccountEmailOptions> emailOptions,
    SystemSettings settings)
{
    public async Task<IdentityResult> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            UserName = email.Trim(),
            DisplayName = displayName.Trim()
        };

        // Validate even duplicate requests without revealing whether an email exists.
        foreach (var validator in users.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(users, user, password);
            if (!validation.Succeeded) return validation;
        }

        if (await users.FindByEmailAsync(user.Email) is not null)
        {
            _ = users.PasswordHasher.HashPassword(user, password);
            return IdentityResult.Success;
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                return result.Errors.All(error => error.Code is "DuplicateEmail" or "DuplicateUserName")
                    ? IdentityResult.Success : result;
            }

            result = await users.AddToRoleAsync(user, RoleNames.Learner);
            if (!result.Succeeded) throw new InvalidOperationException("Could not assign the initial account role.");

            var defaults = (await settings.ReadAsync(cancellationToken)).Defaults;
            database.UserPreferences.Add(new UserPreference
            {
                UserId = user.Id,
                DailyGoalXp = defaults.DailyGoalXp,
                StudyDays = defaults.StudyDays,
                ReminderTime = defaults.ReminderTime
            });
            await QueueConfirmationAsync(user);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            return IdentityResult.Success;
        }
    }

    public async Task ResendAsync(string email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || user.EmailConfirmed || user.Status != "Active") return;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        _ = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"Id\" = {user.Id} FOR UPDATE").ToListAsync(cancellationToken);
        await database.Entry(user).ReloadAsync(cancellationToken);
        if (user.EmailConfirmed || user.Status != "Active") return;
        bool recentlyQueued = await database.EmailOutboxMessages.AnyAsync(message =>
            message.UserId == user.Id && message.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5), cancellationToken);
        if (!recentlyQueued)
        {
            await QueueConfirmationAsync(user);
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<EmailConfirmationResult> ConfirmEmailAsync(Guid userId, string encodedToken, CancellationToken cancellationToken)
    {
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken)); }
        catch (FormatException) { return EmailConfirmationResult.Invalid; }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize confirmation attempts so a link cannot succeed twice in a race.
        var candidates = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").ToListAsync(cancellationToken);
        var user = candidates.SingleOrDefault();
        if (user is null || user.Status != "Active") return EmailConfirmationResult.Invalid;
        if (user.EmailConfirmed)
        {
            // Reveal this state only to someone holding a still-valid confirmation token.
            bool valid = await users.VerifyUserTokenAsync(user, users.Options.Tokens.EmailConfirmationTokenProvider,
                UserManager<ApplicationUser>.ConfirmEmailTokenPurpose, token);
            return valid ? EmailConfirmationResult.AlreadyConfirmed : EmailConfirmationResult.Invalid;
        }

        var result = await users.ConfirmEmailAsync(user, token);
        if (!result.Succeeded) return EmailConfirmationResult.Invalid;
        await transaction.CommitAsync(cancellationToken);
        return EmailConfirmationResult.Confirmed;
    }
    private async Task QueueConfirmationAsync(ApplicationUser user)
    {
        string token = await users.GenerateEmailConfirmationTokenAsync(user);
        string encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        string url = $"{emailOptions.Value.WebOrigin.TrimEnd('/')}/verify-email?userId={user.Id}&token={encoded}";
        database.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            UserId = user.Id,
            Recipient = user.Email ?? throw new InvalidOperationException("Account email is required."),
            Subject = "Your first hello starts here - confirm your LexiGo email",
            HtmlBody = $"<h1>Welcome to LexiGo.</h1><p>Confirm your email to begin your language journey.</p><p><a href=\"{HtmlEncoder.Default.Encode(url)}\">Confirm my email</a></p><p>This link expires in 30 minutes. If you did not request this, ignore this email.</p>"
        });

    }

}
