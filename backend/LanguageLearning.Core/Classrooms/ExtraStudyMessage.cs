namespace LanguageLearning.Core.Classrooms;

public sealed class ExtraStudyMessage
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid SenderId { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
