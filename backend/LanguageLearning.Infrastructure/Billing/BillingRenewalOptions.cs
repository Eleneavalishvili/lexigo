namespace LanguageLearning.Infrastructure.Billing;

public sealed class BillingRenewalOptions
{
    // Explicitly enable only after migrations and the local simulator checks pass.
    public bool Enabled { get; set; }
    public int GraceMinutes { get; set; } = 15;
}
