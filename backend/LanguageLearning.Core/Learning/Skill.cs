namespace LanguageLearning.Core.Learning;

public sealed class Skill
{
    public int Id { get; set; }
    public Guid CourseId { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public bool IsArchived { get; set; }
    public int Revision { get; set; } = 1;
}
