namespace LanguageLearning.Core.Learning;

public sealed class DialogueChoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DialogueId { get; set; }
    public Guid NodeId { get; set; }
    public required string Text { get; set; }
    public required string Feedback { get; set; }
    public bool IsCorrect { get; set; }
    public Guid? NextNodeId { get; set; }
}
