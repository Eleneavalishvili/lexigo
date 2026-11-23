namespace LanguageLearning.Core.Content;

public sealed class AcceptedAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExerciseId { get; set; }
    public required string AnswerText { get; set; }
    public string? OrderedTokenIds { get; set; }
}

