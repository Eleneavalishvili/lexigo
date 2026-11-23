namespace LanguageLearning.Core.Learning;

public sealed class Dialogue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public required string Title { get; set; }
    public required string Scenario { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string Status { get; set; } = "Draft";
    public bool RequiresPremium { get; set; }
}
