namespace LanguageLearning.Core.Classrooms;

public sealed class ExtraStudyLesson
{
    public Guid RequestId { get; set; }
    public Guid LessonId { get; set; }
    public DateTimeOffset SharedAt { get; set; }
}
