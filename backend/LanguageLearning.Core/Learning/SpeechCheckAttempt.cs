namespace LanguageLearning.Core.Learning;

public sealed class SpeechCheckAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SessionExerciseId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public required string AudioSha256 { get; set; }
    public required string Status { get; set; }
    public int HeartsCharged { get; set; }
    public required string ResponseSnapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
