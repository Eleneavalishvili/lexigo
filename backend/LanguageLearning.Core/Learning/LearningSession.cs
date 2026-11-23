namespace LanguageLearning.Core.Learning;

public sealed class LearningSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? LessonId { get; set; }
    public int? PracticeSkillId { get; set; }
    public string? PracticeFocus { get; set; }
    public Guid LessonVersionId { get; set; }
    public required string Title { get; set; }
    public required string Explanation { get; set; }
    public string Mode { get; set; } = "Lesson";
    public int Hearts { get; set; } = 5;
    public int PassScore { get; set; } = 80;
    public int FirstCompletionXp { get; set; } = 20;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int Score { get; set; }
    public int AwardedXp { get; set; }
    public string? PlacementMap { get; set; }
    public Guid? RecommendedUnitId { get; set; }
    public DateTimeOffset? PlacementAppliedAt { get; set; }
}
