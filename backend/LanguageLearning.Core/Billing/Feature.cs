namespace LanguageLearning.Core.Billing;

public sealed class Feature
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Description { get; set; }
}
