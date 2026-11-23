using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed record ActivityDay(DateOnly Date, int EarnedXp, int CompletedLessons, bool GoalReached);
public sealed record ActivityPage(ActivityDay[] Items, int Page, int PageSize, int TotalCount);

public sealed class ActivityQueries(LexiGoDbContext database)
{
    public async Task<ActivityPage> ListAsync(Guid userId, DateOnly from, DateOnly to, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var days = await database.DailyActivities.AsNoTracking()
            .Where(item => item.UserId == userId && item.LocalDate >= from && item.LocalDate <= to)
            .OrderBy(item => item.LocalDate)
            .Select(item => new ActivityDay(item.LocalDate, item.EarnedXp, item.CompletedLessons, item.GoalReached))
            .ToArrayAsync(cancellationToken);
        return new(days.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), page, pageSize, days.Length);
    }
}
