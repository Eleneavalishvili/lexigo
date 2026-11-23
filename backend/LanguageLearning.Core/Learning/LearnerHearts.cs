namespace LanguageLearning.Core.Learning;

public sealed class LearnerHearts
{
    public Guid UserId { get; set; }
    public int Count { get; set; } = 5;
    public DateTimeOffset? RefillAt { get; set; }
}
