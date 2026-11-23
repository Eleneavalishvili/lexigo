namespace LanguageLearning.Core.Courses;

public sealed class Course
{
    public Guid Id { get; set; }
    public int SourceLanguageId { get; set; }
    public int TargetLanguageId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public int OrderNumber { get; set; }
    public Language SourceLanguage { get; set; } = null!;
    public Language TargetLanguage { get; set; } = null!;
}
