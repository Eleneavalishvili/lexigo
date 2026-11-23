namespace LanguageLearning.Core.Administration;

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public string? Reason { get; set; }
    public required string TraceId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
