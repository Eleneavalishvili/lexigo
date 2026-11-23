namespace LanguageLearning.Core.Billing;

public sealed class PaymentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public Guid TestCardId { get; set; }
    public required string Status { get; set; }
    public string? FailureCode { get; set; }
    public Guid IdempotencyKey { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public bool SimulateTechnicalFailure { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
