namespace LanguageLearning.Core.Learning;

public sealed class DialogueSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid DialogueId { get; set; }
    public Guid CurrentNodeId { get; set; }
    public required string VersionSnapshot { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
