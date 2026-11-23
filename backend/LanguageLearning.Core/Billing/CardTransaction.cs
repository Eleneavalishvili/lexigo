namespace LanguageLearning.Core.Billing;

public sealed class CardTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TestCardId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? HeartRefillPurchaseId { get; set; }
    public Guid? RefundId { get; set; }
    public required string Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public required string Reference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
