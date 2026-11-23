namespace LanguageLearning.Core.Billing;

public sealed class BillingOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PlanPriceId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public decimal AmountSnapshot { get; set; }
    public required string Currency { get; set; }
    public required string PeriodSnapshot { get; set; }
    public required string Status { get; set; }
    public required string OrderType { get; set; }
    public string? RenewalPeriodKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
