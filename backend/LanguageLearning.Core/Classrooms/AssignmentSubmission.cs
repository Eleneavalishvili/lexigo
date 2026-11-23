namespace LanguageLearning.Core.Classrooms;

public sealed class AssignmentSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public bool IsLate { get; set; }
}
