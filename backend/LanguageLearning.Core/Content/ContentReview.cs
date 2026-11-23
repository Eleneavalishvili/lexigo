namespace LanguageLearning.Core.Content;

public sealed class ContentReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonVersionId { get; set; }
    public Guid ReviewerId { get; set; }
    public required string Decision { get; set; }
    public required string Comment { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
}

