namespace LanguageLearning.Core.Billing;

public sealed class Refund
{
    public const string ErasedReason = "Personal refund note erased after account deletion.";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public string ReasonFingerprint { get; set; } = "";
    public required string Status { get; set; }
    public Guid IdempotencyKey { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool RevokedPremium { get; set; }
}
