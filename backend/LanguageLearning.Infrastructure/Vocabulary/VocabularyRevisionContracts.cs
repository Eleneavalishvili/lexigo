using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed record VocabularyText(
    [Required, StringLength(160), RegularExpression(@"^[^\u0000]*$")] string Word,
    [Required, StringLength(300), RegularExpression(@"^[^\u0000]*$")] string EnglishTranslation,
    [StringLength(2000), RegularExpression(@"^[^\u0000]*$")] string? Example,
    [StringLength(60), RegularExpression(@"^[^\u0000]*$")] string? PartOfSpeech,
    [RegularExpression("^(greetings|numbers|family|food|travel|routines)$")] string? Topic);

public sealed record VocabularyRevisionView(Guid Id, Guid RevisionToken, Guid? WordId, Guid AuthorId,
    Guid? ReviewerId, Guid? PublisherId, string Status, VocabularyText Text, string? ReviewComment,
    DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt, DateTimeOffset? PublishedAt);
