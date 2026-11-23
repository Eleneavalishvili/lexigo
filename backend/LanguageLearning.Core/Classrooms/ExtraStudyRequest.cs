namespace LanguageLearning.Core.Classrooms;

public sealed class ExtraStudyRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferId { get; set; }
    public Guid LearnerId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
}
