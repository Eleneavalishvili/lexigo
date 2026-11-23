namespace LanguageLearning.Infrastructure.Billing;

public sealed record PlanFeatureView(string Code, string Description, int? LimitValue, bool Available);
public sealed record PlanPriceView(Guid Id, string Period, decimal Amount, string Currency);
public sealed record PlanView(Guid Id, string Code, string Name, PlanPriceView[] Prices, PlanFeatureView[] Features);
public sealed record EntitlementView(string PlanCode, PlanFeatureView[] Features);
public sealed record SubscriptionView(Guid? Id, string PlanCode, string Status, string? Period, DateTimeOffset? CurrentPeriodStart,
    DateTimeOffset? CurrentPeriodEnd, bool CancelAtPeriodEnd, bool AutoRenew, Guid? DefaultTestCardId, Guid? ScheduledPlanPriceId,
    PlanPriceView? ScheduledPlanPrice = null, bool RenewalProcessingAvailable = false, bool RenewalConsentRequired = false,
    Guid? RenewalTargetSubscriptionId = null);
public sealed record BillingOrderView(Guid Id, Guid PlanPriceId, decimal Amount, string Currency, string Period, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, Guid? SubscriptionId);
public sealed record PaymentView(Guid Id, Guid OrderId, Guid TestCardId, string Status, string? FailureCode, decimal Amount,
    string Currency, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, Guid? SubscriptionId, string? ReceiptNumber);
public sealed record OrderOutcome(BillingOrderView? Value = null, bool Missing = false, string? Error = null,
    bool RateLimited = false, bool Created = false);
public sealed record PaymentOutcome(PaymentView? Value = null, bool Missing = false, string? Error = null, bool RateLimited = false);
