using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed record NotificationView(Guid Id, string Type, string Title, string Body, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record NotificationPage(NotificationView[] Items, DateTimeOffset? NextBefore, Guid? NextId);

public sealed class NotificationService(LexiGoDbContext database, TimeProvider clock)
{
    public async Task<NotificationPage> PageAsync(Guid userId, int limit, DateTimeOffset? before, Guid? beforeId, CancellationToken cancellationToken)
    {
        var query = database.Notifications.AsNoTracking().Where(item => item.UserId == userId);
        if (before is { } date && beforeId is { } id)
        {
            date = date.ToUniversalTime();
            query = query.Where(item => item.CreatedAt < date || (item.CreatedAt == date && item.Id.CompareTo(id) < 0));
        }
        var rows = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(limit + 1)
            .Select(item => new NotificationView(item.Id, item.Type, item.Title, item.Body, item.CreatedAt, item.ReadAt)).ToArrayAsync(cancellationToken);
        var items = rows.Take(limit).ToArray();
        return new(items, rows.Length > limit ? items[^1].CreatedAt : null, rows.Length > limit ? items[^1].Id : null);
    }

    public async Task<NotificationView?> ReadAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await database.Notifications.Where(item => item.UserId == userId && item.Id == id && item.ReadAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ReadAt, item => item.CreatedAt > now ? item.CreatedAt : now), cancellationToken);
        return await database.Notifications.AsNoTracking().Where(item => item.UserId == userId && item.Id == id)
            .Select(item => new NotificationView(item.Id, item.Type, item.Title, item.Body, item.CreatedAt, item.ReadAt)).SingleOrDefaultAsync(cancellationToken);
    }
}
