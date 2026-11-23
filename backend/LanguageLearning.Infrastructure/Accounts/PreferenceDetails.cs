namespace LanguageLearning.Infrastructure.Accounts;

public sealed record PreferenceDetails(string TimeZone, int DailyGoalXp, string Theme,
    bool ReduceMotion, bool SoundEnabled, bool LeaderboardOptIn, Guid? ActiveCourseId,
    int StudyDays, TimeOnly ReminderTime, bool EmailRemindersEnabled);

public sealed record CourseChoice(Guid Id, string Title);
