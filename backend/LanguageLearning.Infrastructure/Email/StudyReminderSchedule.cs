using LanguageLearning.Core.Accounts;

namespace LanguageLearning.Infrastructure.Email;

public static class StudyReminderSchedule
{
    public static DateOnly? DueOn(UserPreference preference, DateTimeOffset now)
    {
        if (!preference.EmailRemindersEnabled ||
            !TimeZoneInfo.TryFindSystemTimeZoneById(preference.TimeZone, out var zone))
            return null;

        var local = TimeZoneInfo.ConvertTime(now, zone).DateTime;
        int day = ((int)local.DayOfWeek + 6) % 7;
        if ((preference.StudyDays & (1 << day)) == 0) return null;

        var date = DateOnly.FromDateTime(local);
        var due = date.ToDateTime(preference.ReminderTime);
        // A clock change can skip the chosen wall-clock time.
        while (zone.IsInvalidTime(due) && DateOnly.FromDateTime(due) == date)
            due = due.AddMinutes(1);

        return local >= due && local < due.AddMinutes(30) ? date : null;
    }
}
