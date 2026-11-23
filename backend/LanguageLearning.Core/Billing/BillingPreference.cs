namespace LanguageLearning.Core.Billing;

public sealed class BillingPreference
{
    public Guid UserId { get; set; }
    public Guid? DefaultTestCardId { get; set; }
    public bool AutoRenew { get; set; }
    public Guid? RenewalConsentSubscriptionId { get; set; }
    public DateTimeOffset? RenewalConsentAt { get; set; }
}
