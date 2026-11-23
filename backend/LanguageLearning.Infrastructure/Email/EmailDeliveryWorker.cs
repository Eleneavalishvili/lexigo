using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LanguageLearning.Infrastructure.Email;

public sealed class EmailDeliveryWorker(
    IServiceScopeFactory scopes,
    IOptions<SmtpOptions> options,
    IHostEnvironment environment,
    ILogger<EmailDeliveryWorker> logger,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.DeliveryEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await DeliverNextAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                // Do not log message bodies, recipients or confirmation tokens.
                logger.LogWarning("Email delivery temporarily unavailable ({ExceptionType}).", exception.GetType().Name);
            }
        }
    }

    private async Task DeliverNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var messages = await database.EmailOutboxMessages.FromSqlInterpolated(
            $"SELECT * FROM \"EmailOutboxMessages\" WHERE \"SentAt\" IS NULL AND \"Attempts\" < 8 AND (\"NextAttemptAt\" IS NULL OR \"NextAttemptAt\" <= {DateTimeOffset.UtcNow}) ORDER BY \"CreatedAt\" LIMIT 1 FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
        var message = messages.SingleOrDefault();
        if (message is null) return;

        var reminder = message.Purpose == StudyReminderQueue.Purpose
            ? await database.ReminderDeliveries.FindAsync([message.Id], cancellationToken)
            : null;
        if (message.Purpose == StudyReminderQueue.Purpose)
        {
            // Persist the terminal reservation before SMTP. A crash can leave the
            // outcome unconfirmed, but another worker must never retry this reminder.
            var email = reminder is null ? null : BuildEmail(message);
            if (reminder is not null) reminder.Status = "Failed";
            message.Attempts = 8;
            message.HtmlBody = "";
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (reminder is not null && email is not null)
                await SendReservedReminderAsync(database, message, reminder, email, cancellationToken);
            return;
        }

        bool accountExists = await database.Users.AsNoTracking()
            .AnyAsync(user => user.Id == message.UserId && user.Status != "Deleted", cancellationToken);
        if (!accountExists)
        {
            message.Attempts = 8;
            message.Recipient = "";
            message.HtmlBody = "";
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        message.Attempts++;
        try
        {
            await SendAsync(BuildEmail(message), cancellationToken);
            message.SentAt = clock.GetUtcNow();
            message.HtmlBody = "";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            message.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(Math.Min(message.Attempts * 2, 30));
            logger.LogWarning("Email outbox item {MessageId} could not be delivered; retry scheduled.", message.Id);
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SendReservedReminderAsync(
        LexiGoDbContext database, EmailOutboxMessage message,
        ReminderDelivery reminder, MimeMessage email, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await CanSendReminderAsync(database, message, reminder.LocalDate, cancellationToken))
        {
            reminder.Status = "Cancelled";
        }
        else
        {
            await SendAsync(email, cancellationToken);
            message.SentAt = clock.GetUtcNow();
            reminder.SentAt = message.SentAt;
            reminder.Status = "Sent";
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private MimeMessage BuildEmail(EmailOutboxMessage message)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress("LexiGo", options.Value.SenderAddress));
        email.To.Add(MailboxAddress.Parse(message.Recipient));
        email.Subject = message.Subject;
        email.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();
        return email;
    }

    private async Task SendAsync(MimeMessage email, CancellationToken cancellationToken)
    {
        using var smtp = new SmtpClient { Timeout = 10_000 };
        bool localDevelopment = environment.IsDevelopment() && options.Value.Host is "localhost" or "127.0.0.1";
        await smtp.ConnectAsync(options.Value.Host, options.Value.Port,
            localDevelopment ? SecureSocketOptions.None : SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrWhiteSpace(options.Value.Username))
            await smtp.AuthenticateAsync(options.Value.Username, options.Value.Password ?? throw new InvalidOperationException("Configure the SMTP password."), cancellationToken);
        await smtp.SendAsync(email, cancellationToken);
    }

    private async Task<bool> CanSendReminderAsync(
        LexiGoDbContext database, EmailOutboxMessage message, DateOnly localDate, CancellationToken cancellationToken)
    {
        var user = await database.Users.FromSqlInterpolated(
                $"""SELECT * FROM "Users" WHERE "Id" = {message.UserId} FOR UPDATE""")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (user is not { EmailConfirmed: true, Status: "Active" } ||
            !string.Equals(user.Email, message.Recipient, StringComparison.OrdinalIgnoreCase))
            return false;

        var preference = await database.UserPreferences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == message.UserId, cancellationToken);
        return preference is not null && StudyReminderSchedule.DueOn(preference, clock.GetUtcNow()) == localDate;
    }
}

