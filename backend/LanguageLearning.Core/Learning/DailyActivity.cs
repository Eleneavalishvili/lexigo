namespace LanguageLearning.Core.Learning;

public sealed class DailyActivity
{
    public Guid UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public int EarnedXp { get; set; }
    public int CompletedLessons { get; set; }
    public bool GoalReached { get; set; }
}
