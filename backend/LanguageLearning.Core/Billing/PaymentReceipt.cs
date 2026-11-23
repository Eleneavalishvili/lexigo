namespace LanguageLearning.Core.Billing;

public sealed class PaymentReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public required string ReceiptNumber { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
}
