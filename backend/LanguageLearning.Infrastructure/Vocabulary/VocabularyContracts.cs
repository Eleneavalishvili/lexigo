namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed record WordView(Guid Id, Guid CourseId, string CourseTitle, string LanguageCode,
    string Word, string EnglishTranslation, string Example, string PartOfSpeech,
    Guid? SavedWordId, DateTimeOffset? DueAt, int? IntervalDays, string? Topic, bool HasAudio);
public sealed record WordPage(WordView[] Items, int Page, int PageSize, int TotalCount);
public sealed record WordAudioLink(string Url, int ExpiresInSeconds, string Source, string License);
public sealed record SaveWordResult(Guid Id, Guid VocabularyItemId, DateTimeOffset DueAt, int IntervalDays, int CorrectStreak);
public sealed record VocabularyOutcome(object? Value = null, bool Missing = false, string? Error = null);
