using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Email;

public sealed class StudyReminderQueue(LexiGoDbContext database, TimeProvider clock)
{
    public const string Purpose = "StudyReminder";
    public const string Type = "StudyEmail";

    public async Task QueueDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates =
            from preference in database.UserPreferences.AsNoTracking()
            join user in database.Users on preference.UserId equals user.Id
            where preference.Reminder.Enabled && user.EmailConfirmed && user.Status == "Active"
            select preference;

        const int pageSize = 250;
        Guid? lastUserId = null;
        while (true)
        {
            var page = lastUserId is { } cursor
                ? candidates.Where(item => item.UserId.CompareTo(cursor) > 0)
                : candidates;
            var batch = await page.OrderBy(item => item.UserId).Take(pageSize).ToListAsync(cancellationToken);
            foreach (var candidate in batch)
            {
                if (StudyReminderSchedule.DueOn(candidate, now) is not null)
                    await QueueForUserAsync(candidate.UserId, cancellationToken);
            }

            if (batch.Count < pageSize) return;
            lastUserId = batch[^1].UserId;
        }
    }

    private async Task QueueForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Preference edits and account changes use the same user lock.
        var user = await database.Users.FromSqlInterpolated(
                $"""SELECT * FROM "Users" WHERE "Id" = {userId} FOR UPDATE""")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (user is not { EmailConfirmed: true, Status: "Active", Email: { } email }) return;

        var preference = await database.UserPreferences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var now = clock.GetUtcNow();
        if (preference is null || StudyReminderSchedule.DueOn(preference, now) is not { } date) return;
        if (await database.ReminderDeliveries.AnyAsync(
                item => item.UserId == userId && item.LocalDate == date && item.Type == Type,
                cancellationToken)) return;

        var message = new EmailOutboxMessage
        {
            UserId = userId,
            Purpose = Purpose,
            Recipient = email,
            Subject = "Time for a little LexiGo practice",
            HtmlBody = "<p>It’s time for the language practice you scheduled in LexiGo.</p><p>You can change your schedule or turn off these emails in your profile settings.</p>",
            CreatedAt = now,
        };
        database.EmailOutboxMessages.Add(message);
        database.ReminderDeliveries.Add(new ReminderDelivery
        {
            Id = message.Id,
            UserId = userId,
            LocalDate = date,
            Type = Type,
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
    }
}
