namespace LanguageLearning.Core.Billing;

public sealed class SubscriptionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public required string EventType { get; set; }
    public Guid? RelatedPaymentId { get; set; }
    public Guid? RefundId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ActorId { get; set; }
}
