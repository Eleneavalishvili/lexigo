using System.Net.Http.Json;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class ReminderPreferenceMigrationTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task PreservesOptInMidnightAndNonAdjacentDaysAcrossMigration()
    {
        var email = $"reminder-migration-{Guid.NewGuid():N}@example.test";
        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Migration Tester",
            password = "A long test passphrase 42!",
        });
        response.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var userId = await database.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        var profile = await database.UserPreferences.SingleAsync(item => item.UserId == userId);
        profile.EmailRemindersEnabled = true;
        profile.ReminderTime = TimeOnly.MinValue;
        profile.StudyDays = 65;
        await database.SaveChangesAsync();

        await database.Database.MigrateAsync("20261104140009_AddStudyReminderDeliveries");
        await database.Database.MigrateAsync();

        var restored = await database.UserPreferences.AsNoTracking().SingleAsync(item => item.UserId == userId);
        Assert.True(restored.EmailRemindersEnabled);
        Assert.Equal(TimeOnly.MinValue, restored.ReminderTime);
        Assert.Equal(new[] { 0, 6 }, restored.Reminder.WeekDays);
    }
}
