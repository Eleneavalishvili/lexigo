namespace LanguageLearning.Core.Content;

public sealed class Exercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonVersionId { get; set; }
    public required string Type { get; set; }
    public required string Prompt { get; set; }
    public string? ContextText { get; set; }
    public required string Explanation { get; set; }
    public int OrderNumber { get; set; }
    public Guid? AudioMediaId { get; set; }
    public string EvaluationRules { get; set; } = "{}";
}

