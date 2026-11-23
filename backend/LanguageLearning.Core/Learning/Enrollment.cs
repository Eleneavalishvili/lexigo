namespace LanguageLearning.Core.Learning;

public sealed class Enrollment
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public DateTimeOffset EnrolledAt { get; set; }
    public string? PlacementLevel { get; set; }
}
