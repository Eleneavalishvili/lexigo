namespace LanguageLearning.Core.Vocabulary;

public sealed class WordReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SavedWordId { get; set; }
    public bool IsCorrect { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
    public Guid IdempotencyKey { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public int IntervalDays { get; set; }
}
