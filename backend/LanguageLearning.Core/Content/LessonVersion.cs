namespace LanguageLearning.Core.Content;

public sealed class LessonVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public int VersionNumber { get; set; }
    public required string Title { get; set; }
    public required string Objective { get; set; }
    public required string Explanation { get; set; }
    public Guid CreatedBy { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? PrivateTextErasedAt { get; set; }
}

