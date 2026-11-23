namespace LanguageLearning.Core.Content;

public sealed class LessonPrerequisite
{
    public Guid LessonId { get; set; }
    public Guid PrerequisiteLessonId { get; set; }
}

