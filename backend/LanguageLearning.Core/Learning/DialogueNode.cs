namespace LanguageLearning.Core.Learning;

public sealed class DialogueNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DialogueId { get; set; }
    public required string Text { get; set; }
    public Guid? AudioMediaId { get; set; }
    public bool IsStart { get; set; }
    public bool IsTerminal { get; set; }
}
