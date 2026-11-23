namespace LanguageLearning.Core.Vocabulary;

public sealed class SavedWord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid VocabularyItemId { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public int IntervalDays { get; set; }
    public int CorrectStreak { get; set; }
}
