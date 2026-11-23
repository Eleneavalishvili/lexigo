namespace LanguageLearning.Core.Billing;

public sealed class PlanFeature
{
    public Guid PlanId { get; set; }
    public int FeatureId { get; set; }
    public int? LimitValue { get; set; }
}
