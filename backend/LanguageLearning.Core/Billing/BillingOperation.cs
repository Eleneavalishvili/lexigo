namespace LanguageLearning.Core.Billing;

// Durable replay evidence for simulated wallet and subscription preference operations.
public sealed class BillingOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Operation { get; set; }
    public Guid IdempotencyKey { get; set; }
    public required string RequestSnapshot { get; set; }
    public required string ResponseSnapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
