using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class ProfileService(LexiGoDbContext database)
{
    public async Task<bool> SetStudyGoalAsync(Guid userId, int dailyGoalXp, CancellationToken cancellationToken)
    {
        if (dailyGoalXp is < 1 or > 1000) return false;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return false;
        await database.Entry(user).ReloadAsync(cancellationToken);
        if (user.Status != "Active") return false;
        var preference = await database.UserPreferences.SingleAsync(item => item.UserId == userId, cancellationToken);
        preference.DailyGoalXp = dailyGoalXp;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
    public async Task<string?> RenameAsync(Guid userId, string displayName, CancellationToken cancellationToken)
    {
        string name = displayName.Trim();
        if (name.Length is < 2 or > 80 || name.Any(char.IsControl)) return null;
        int changed = await database.Users.Where(user => user.Id == userId && user.Status == "Active")
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.DisplayName, name), cancellationToken);
        return changed == 1 ? name : null;
    }

    public async Task<PreferenceDetails?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var item = await database.UserPreferences.AsNoTracking()
            .SingleOrDefaultAsync(preference => preference.UserId == userId, cancellationToken);
        return item is null ? null : new PreferenceDetails(item.TimeZone, item.DailyGoalXp, item.Theme,
            item.ReduceMotion, item.SoundEnabled, item.LeaderboardOptIn, item.ActiveCourseId,
            item.StudyDays, item.ReminderTime, item.EmailRemindersEnabled);
    }

    public async Task<IReadOnlyList<CourseChoice>> CoursesAsync(CancellationToken cancellationToken) =>
        await database.Courses.AsNoTracking().Where(course => course.IsPublished)
            .OrderBy(course => course.OrderNumber).Select(course => new CourseChoice(course.Id, course.Title))
            .ToArrayAsync(cancellationToken);

    public async Task<bool> SaveAsync(Guid userId, PreferenceDetails value, CancellationToken cancellationToken)
    {
        if (value.DailyGoalXp is < 1 or > 1000 || value.StudyDays is < 1 or > 127
            || value.Theme is not ("system" or "light" or "dark")
            || string.IsNullOrWhiteSpace(value.TimeZone) || value.TimeZone.Length > 100
            || !TimeZoneInfo.TryFindSystemTimeZoneById(value.TimeZone, out _)
            || value.ReminderTime.Second != 0 || value.ReminderTime.Millisecond != 0) return false;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Match session/security mutations' user-first lock order.
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is not null) await database.Entry(user).ReloadAsync(cancellationToken);
        if (user is null || user.Status != "Active") return false;
        if (value.ActiveCourseId is { } courseId && !await database.Courses
                .AnyAsync(course => course.Id == courseId && course.IsPublished, cancellationToken)) return false;
        var preference = await database.UserPreferences.SingleAsync(item => item.UserId == userId, cancellationToken);
        preference.TimeZone = value.TimeZone;
        preference.DailyGoalXp = value.DailyGoalXp;
        preference.Theme = value.Theme;
        preference.ReduceMotion = value.ReduceMotion;
        preference.SoundEnabled = value.SoundEnabled;
        preference.LeaderboardOptIn = value.LeaderboardOptIn;
        if (!value.LeaderboardOptIn) preference.SharedLeaderboardAvatarId = null;
        preference.ActiveCourseId = value.ActiveCourseId;
        preference.StudyDays = value.StudyDays;
        preference.ReminderTime = value.ReminderTime;
        preference.EmailRemindersEnabled = value.EmailRemindersEnabled;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
