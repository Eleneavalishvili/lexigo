namespace LanguageLearning.Core.Classrooms;

public sealed class ExtraStudyOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeacherId { get; set; }
    public Guid CourseId { get; set; }
    public bool Enabled { get; set; }
}
