namespace LanguageLearning.Core.Learning;

public sealed class CompletionCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public Guid UnitId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public required string VerificationCode { get; set; }
    public required string CourseTitle { get; set; }
    public required string UnitTitle { get; set; }
    public int LessonCount { get; set; }
    public int GradedLessonCount { get; set; }
    public decimal? AverageBestScore { get; set; }
    public bool IsShared { get; set; }
    public string? ShareCode { get; set; }
    public int Revision { get; set; } = 1;
}
