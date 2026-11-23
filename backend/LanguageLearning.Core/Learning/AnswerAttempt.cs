namespace LanguageLearning.Core.Learning;

public sealed class AnswerAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SessionExerciseId { get; set; }
    public required string AnswerPayload { get; set; }
    public required string ResponseSnapshot { get; set; }
    public bool IsCorrect { get; set; }
    public bool IsGraded { get; set; } = true;
    public Guid IdempotencyKey { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
}
