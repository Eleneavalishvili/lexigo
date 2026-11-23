namespace LanguageLearning.Core.Billing;

public sealed class BillingJobExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public required string PeriodKey { get; set; }
    public required string Status { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}
