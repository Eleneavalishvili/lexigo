namespace LanguageLearning.Core.Content;

public sealed class Unit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public int OrderNumber { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string Difficulty { get; set; } = "Beginner";
}

