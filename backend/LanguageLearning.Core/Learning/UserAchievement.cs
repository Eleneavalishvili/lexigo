namespace LanguageLearning.Core.Learning;

public sealed class UserAchievement
{
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public DateTimeOffset EarnedAt { get; set; }
}
