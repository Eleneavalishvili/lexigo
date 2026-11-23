namespace LanguageLearning.Core.Learning;

public sealed class LessonProgress
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public string Status { get; set; } = "InProgress";
    public int BestScore { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
