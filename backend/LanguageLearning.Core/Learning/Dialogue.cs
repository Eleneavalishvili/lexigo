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
    public Guid? OwnerId { get; set; }
    public Guid? ReviewerId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public Guid EditToken { get; set; } = Guid.NewGuid();
}
