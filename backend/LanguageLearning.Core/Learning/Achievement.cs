namespace LanguageLearning.Core.Learning;

public sealed class Achievement
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Title { get; set; }
    public required string RuleType { get; set; }
    public int Threshold { get; set; }
    public bool IsArchived { get; set; }
    public int Revision { get; set; } = 1;
}
