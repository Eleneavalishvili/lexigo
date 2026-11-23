using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class StudyReminderTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task ConcurrentSchedulersQueueOneReminderForTheEligibleLocalDate()
    {
        var email = $"reminder-{Guid.NewGuid():N}@example.test";
        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Reminder Tester",
            password = "A long test passphrase 42!",
        });
        response.EnsureSuccessStatusCode();
        await using var setup = fixture.Services.CreateAsyncScope();
        var database = setup.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var user = await database.Users.SingleAsync(item => item.Email == email);
        var preference = await database.UserPreferences.SingleAsync(item => item.UserId == user.Id);
        user.EmailConfirmed = true;
        preference.TimeZone = "Asia/Tbilisi";
        preference.StudyDays = 1;
        preference.ReminderTime = new TimeOnly(0, 5);
        await database.SaveChangesAsync();

        var clock = new FixedClock(new DateTimeOffset(2026, 11, 1, 20, 5, 0, TimeSpan.Zero));
        async Task QueueAsync()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await new StudyReminderQueue(context, clock).QueueDueAsync(CancellationToken.None);
        }

        await QueueAsync();
        Assert.False(await database.ReminderDeliveries.AnyAsync(item => item.UserId == user.Id));
        preference.EmailRemindersEnabled = true;
        await database.SaveChangesAsync();

        await Task.WhenAll(QueueAsync(), QueueAsync());

        var delivery = await database.ReminderDeliveries.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(new DateOnly(2026, 11, 2), delivery.LocalDate);
        Assert.Equal("Queued", delivery.Status);
        var emailMessage = await database.EmailOutboxMessages.SingleAsync(
            item => item.UserId == user.Id && item.Purpose == StudyReminderQueue.Purpose);
        Assert.Equal(delivery.Id, emailMessage.Id);
        Assert.Equal(email, emailMessage.Recipient);
    }

    [Theory]
    [InlineData(6, 59, false)]
    [InlineData(7, 0, true)]
    [InlineData(7, 29, true)]
    [InlineData(7, 30, false)]
    public void MissingSpringTimeUsesFirstValidMinute(int hour, int minute, bool due)
    {
        var preference = new UserPreference
        {
            EmailRemindersEnabled = true,
            TimeZone = "America/New_York",
            StudyDays = 64,
            ReminderTime = new TimeOnly(2, 30),
        };
        var now = new DateTimeOffset(2026, 3, 8, hour, minute, 0, TimeSpan.Zero);
        Assert.Equal(due ? new DateOnly(2026, 3, 8) : (DateOnly?)null,
            StudyReminderSchedule.DueOn(preference, now));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
