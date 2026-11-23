namespace LanguageLearning.Core.Content;

public sealed class ContentReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public int LanguageId { get; set; }
    public required string ResourceType { get; set; }
    public Guid ResourceId { get; set; }
    public Guid? VersionId { get; set; }
    public required string Reason { get; set; }
    public required string Comment { get; set; }
    public string Status { get; set; } = "New";
    public Guid? ResolvedBy { get; set; }
    public string? Resolution { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
