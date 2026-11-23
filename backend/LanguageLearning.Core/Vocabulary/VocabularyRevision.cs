namespace LanguageLearning.Core.Vocabulary;

public sealed class VocabularyRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RevisionToken { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid? WordId { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? ReviewerId { get; set; }
    public Guid? PublisherId { get; set; }
    public string Word { get; set; } = "";
    public string EnglishTranslation { get; set; } = "";
    public string Example { get; set; } = "";
    public string PartOfSpeech { get; set; } = "";
    public string? Topic { get; set; }
    public string Status { get; set; } = "Draft";
    public string? ReviewComment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
