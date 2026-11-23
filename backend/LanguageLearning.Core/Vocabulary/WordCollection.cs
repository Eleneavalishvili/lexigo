namespace LanguageLearning.Core.Vocabulary;

public sealed class WordCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public required string Name { get; set; }
}

public sealed class CollectionWord
{
    public Guid CollectionId { get; set; }
    public Guid SavedWordId { get; set; }
}
