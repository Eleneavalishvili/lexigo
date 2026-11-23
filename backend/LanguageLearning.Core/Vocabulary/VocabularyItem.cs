namespace LanguageLearning.Core.Vocabulary;

public sealed class VocabularyItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public string Word { get; set; } = "";
    public string EnglishTranslation { get; set; } = "";
    public string Example { get; set; } = "";
    public Guid? AudioMediaId { get; set; }
    public string PartOfSpeech { get; set; } = "";
    public string? Topic { get; set; }
}
