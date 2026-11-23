namespace LanguageLearning.Core.Billing;

public sealed class TestCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Nickname { get; set; }
    public required string DisplayLastFour { get; set; }
    public required string Currency { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; } = "Active";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
}
