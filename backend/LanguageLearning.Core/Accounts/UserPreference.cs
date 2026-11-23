namespace LanguageLearning.Core.Accounts;

public sealed class UserPreference
{
    public Guid UserId { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public int DailyGoalXp { get; set; } = 20;
    public string Theme { get; set; } = "light";
    public bool ReduceMotion { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool LeaderboardOptIn { get; set; }
    public Guid? SharedLeaderboardAvatarId { get; set; }
    public Guid? ActiveCourseId { get; set; }
    public ReminderPreference Reminder { get; set; } = new();
    public int StudyDays { get => Reminder.StudyDays; set => Reminder.StudyDays = value; }
    public TimeOnly ReminderTime { get => Reminder.LocalTime; set => Reminder.LocalTime = value; }
    public bool EmailRemindersEnabled { get => Reminder.Enabled; set => Reminder.Enabled = value; }
}
