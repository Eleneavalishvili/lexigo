namespace LanguageLearning.Core.Classrooms;

public sealed class ClassMembership
{
    public Guid ClassroomId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public string Status { get; set; } = "Active";
}
