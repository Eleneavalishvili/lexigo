namespace LanguageLearning.Core.Learning;

public sealed class DialogueAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DialogueId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ChoiceId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
}
