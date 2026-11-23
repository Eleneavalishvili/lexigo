namespace LanguageLearning.Core.Content;

public sealed class Lesson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UnitId { get; set; }
    public Guid OwnerId { get; set; }
    public required string Title { get; set; }
    public required string Objective { get; set; }
    public int OrderNumber { get; set; }
    public string Status { get; set; } = "Draft";
    public Guid? CurrentVersionId { get; set; }
}

