namespace LanguageLearning.Core.Billing;

// One-time heart-product evidence; subscription billing remains a separate required workflow.
public sealed class HeartRefillPurchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TestCardId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public required string OfferCode { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public string? FailureCode { get; set; }
    public required string Reference { get; set; }
    public int HeartsBefore { get; set; }
    public int HeartsAfter { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
