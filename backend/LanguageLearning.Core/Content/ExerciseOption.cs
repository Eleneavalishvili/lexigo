namespace LanguageLearning.Core.Content;

public sealed class ExerciseOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExerciseId { get; set; }
    public required string Text { get; set; }
    public int OrderNumber { get; set; }
    public string? MatchKey { get; set; }
    public Guid? AudioMediaId { get; set; }
}

