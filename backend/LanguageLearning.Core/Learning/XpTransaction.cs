namespace LanguageLearning.Core.Learning;

public sealed class XpTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public required string SourceType { get; set; }
    public Guid SourceId { get; set; }
    public int Amount { get; set; }
    public DateTimeOffset EarnedAt { get; set; }
}
