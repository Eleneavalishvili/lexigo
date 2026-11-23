namespace LanguageLearning.Core.Learning;

public sealed class UserChallenge
{
    public Guid UserId { get; set; }
    public Guid ChallengeId { get; set; }
    public int Progress { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? RewardedAt { get; set; }
}
