namespace LanguageLearning.Core.Classrooms;

public sealed class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassroomId { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public int MinimumScore { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid RequestKey { get; set; }
    public string RequestFingerprint { get; set; } = "";
    public int Revision { get; set; } = 1;
}
