namespace LanguageLearning.Core.Learning;

public sealed class Challenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required string RuleType { get; set; }
    public int Target { get; set; }
    public int RewardXp { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsArchived { get; set; }
    public int Revision { get; set; } = 1;
}
