namespace LanguageLearning.Core.Classrooms;

public sealed class Classroom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeacherId { get; set; }
    public Guid CourseId { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public bool IsArchived { get; set; }
    public int Revision { get; set; } = 1;
    public Guid CreationKey { get; set; } = Guid.NewGuid();
    public string CreationFingerprint { get; set; } = "";
}
