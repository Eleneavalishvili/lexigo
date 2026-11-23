namespace LanguageLearning.Core.Learning;

public sealed class SessionExercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ExerciseId { get; set; }
    public Guid? AudioMediaId { get; set; }
    public int OrderNumber { get; set; }
    public required string PublicSnapshot { get; set; }
    public required string PrivateEvaluationSnapshot { get; set; }
}
