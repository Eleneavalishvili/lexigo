using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class WeeklyChallengeCatalog(LexiGoDbContext database, TimeProvider clock)
{
    public Task EnsureCurrentWeekAsync(CancellationToken cancellationToken) =>
        EnsureWeekAsync(clock.GetUtcNow(), cancellationToken);

    internal async Task EnsureWeekAsync(DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        var today = completedAt.UtcDateTime.Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var start = new DateTimeOffset(monday, TimeSpan.Zero);
        var end = start.AddDays(7);
        await InsertAsync("CompletedLessons", "Three new chapters", 20, start, end, cancellationToken);
        await InsertAsync("StudyDays", "Make a little time", 30, start, end, cancellationToken);
    }

    private async Task InsertAsync(string rule, string title, int reward,
        DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        // Stable IDs let multiple application instances initialize the same week safely.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("LexiGo/weekly/" + start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "/" + rule));
        var id = new Guid(hash.AsSpan(0, 16));
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Challenges" ("Id", "Title", "RuleType", "Target", "RewardXp", "StartsAt", "EndsAt", "IsArchived")
            VALUES ({id}, {title}, {rule}, 3, {reward}, {start}, {end}, false)
            ON CONFLICT DO NOTHING
            """, cancellationToken);
    }
}
