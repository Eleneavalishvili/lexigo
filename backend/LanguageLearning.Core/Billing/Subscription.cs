namespace LanguageLearning.Core.Billing;

public sealed class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public Guid CurrentPlanPriceId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CurrentPeriodStart { get; set; }
    public DateTimeOffset CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public Guid? ScheduledPlanPriceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
