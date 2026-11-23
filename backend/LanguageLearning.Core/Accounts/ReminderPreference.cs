namespace LanguageLearning.Core.Accounts;

public sealed class ReminderPreference
{
    public const string EmailChannel = "Email";

    public Guid UserId { get; set; }
    public string Channel { get; private set; } = EmailChannel;
    public bool Enabled { get; set; }
    public TimeOnly LocalTime { get; set; } = new(18, 0);
    public int[] WeekDays { get; set; } = [0, 1, 2, 3, 4];

    public int StudyDays
    {
        get => WeekDays.Aggregate(0, (mask, day) => mask | (1 << day));
        set => WeekDays = Enumerable.Range(0, 7).Where(day => (value & (1 << day)) != 0).ToArray();
    }
}
