using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Tests.Accounts;

public sealed class StudyReminderDeliveryTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task DeliversOneReminderAndSkipsOptOutAndInterruptedAttempt()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var cancellationToken = timeout.Token;
        await using var inbox = new ContainerBuilder("axllent/mailpit:v1.27")
            .WithPortBinding(1025, true)
            .WithPortBinding(8025, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8025)))
            .Build();
        await inbox.StartAsync(cancellationToken);

        var clock = new FixedClock(new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero));
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var optedIn = AddLearner(database);
        var optedOut = AddLearner(database);
        var interrupted = AddLearner(database);
        var inactive = AddLearner(database);
        var blockedAccount = AddLearner(database);
        blockedAccount.Status = "Blocked";
        database.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            UserId = blockedAccount.Id, Purpose = "SecurityNotification", Recipient = blockedAccount.Email!,
            Subject = "Blocked account security notice", HtmlBody = "Security update", CreatedAt = clock.GetUtcNow()
        });
        database.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            UserId = inactive.Id,
            Purpose = "PasswordReset",
            Recipient = inactive.Email!,
            Subject = "Inactive account reset",
            HtmlBody = "Private reset token",
            CreatedAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        inactive.Status = "Deleted";
        await database.SaveChangesAsync(cancellationToken);

        await new StudyReminderQueue(database, clock).QueueDueAsync(cancellationToken);
        Assert.Equal(3, await database.ReminderDeliveries.CountAsync(cancellationToken));
        var preference = await database.UserPreferences.SingleAsync(
            item => item.UserId == optedOut.Id, cancellationToken);
        preference.EmailRemindersEnabled = false;
        var interruptedDelivery = await database.ReminderDeliveries.SingleAsync(
            item => item.UserId == interrupted.Id, cancellationToken);
        var interruptedMessage = await database.EmailOutboxMessages.SingleAsync(
            item => item.Id == interruptedDelivery.Id, cancellationToken);
        // Model a process that stopped after committing its reservation.
        interruptedDelivery.Status = "Failed";
        interruptedMessage.Attempts = 8;
        interruptedMessage.HtmlBody = "";
        await database.SaveChangesAsync(cancellationToken);

        using var worker = new EmailDeliveryWorker(
            fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SmtpOptions { Host = "localhost", Port = inbox.GetMappedPublicPort(1025) }),
            fixture.Services.GetRequiredService<IHostEnvironment>(),
            NullLogger<EmailDeliveryWorker>.Instance,
            clock);
        await worker.StartAsync(cancellationToken);
        try
        {
            while (await database.ReminderDeliveries.CountAsync(
                       item => item.Status == "Sent" || item.Status == "Cancelled", cancellationToken) < 2)
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

            var deliveries = await database.ReminderDeliveries.AsNoTracking().ToListAsync(cancellationToken);
            var sent = Assert.Single(deliveries, item => item.UserId == optedIn.Id);
            Assert.Equal("Sent", sent.Status);
            Assert.Equal(clock.GetUtcNow(), sent.SentAt);
            var cancelled = Assert.Single(deliveries, item => item.UserId == optedOut.Id);
            Assert.Equal("Cancelled", cancelled.Status);
            Assert.Null(cancelled.SentAt);
            while (await database.EmailOutboxMessages.AsNoTracking().AnyAsync(
                       item => item.UserId == inactive.Id && item.Attempts < 8, cancellationToken))
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            var suppressed = await database.EmailOutboxMessages.AsNoTracking().SingleAsync(
                item => item.UserId == inactive.Id, cancellationToken);
            Assert.Equal("", suppressed.Recipient);
            Assert.Equal("", suppressed.HtmlBody);
            Assert.Null(suppressed.SentAt);
            while (await database.EmailOutboxMessages.AsNoTracking().AnyAsync(
                       item => item.UserId == blockedAccount.Id && item.SentAt == null, cancellationToken))
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

            using var http = new HttpClient();
            using var result = await http.GetFromJsonAsync<JsonDocument>(
                $"http://localhost:{inbox.GetMappedPublicPort(8025)}/api/v1/messages", cancellationToken);
            var messages = result!.RootElement.GetProperty("messages").EnumerateArray().ToArray();
            Assert.Equal(2, messages.Length);
            var message = Assert.Single(messages, item => item.GetProperty("Subject").GetString() == "Time for a little LexiGo practice");
            Assert.Equal("Time for a little LexiGo practice", message.GetProperty("Subject").GetString());
            var recipient = Assert.Single(message.GetProperty("To").EnumerateArray());
            Assert.Equal(optedIn.Email, recipient.GetProperty("Address").GetString());
            var securityNotice = Assert.Single(messages, item => item.GetProperty("Subject").GetString() == "Blocked account security notice");
            Assert.Equal(blockedAccount.Email, Assert.Single(securityNotice.GetProperty("To").EnumerateArray()).GetProperty("Address").GetString());
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            await worker.StopAsync(stopTimeout.Token);
        }
    }

    private static ApplicationUser AddLearner(LexiGoDbContext database)
    {
        var id = Guid.NewGuid();
        var email = $"reminder-{id:N}@example.test";
        var learner = new ApplicationUser
        {
            Id = id,
            DisplayName = "Reminder Tester",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
        };
        database.Users.Add(learner);
        database.UserPreferences.Add(new UserPreference
        {
            UserId = id,
            EmailRemindersEnabled = true,
            StudyDays = 127,
        });
        return learner;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
