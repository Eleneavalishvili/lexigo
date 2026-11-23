namespace LanguageLearning.Core.Content;

public sealed class ImportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthorId { get; set; }
    public required string Status { get; set; }
    public required string FileKey { get; set; }
    public required string ValidationErrors { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? LessonId { get; set; }
}
