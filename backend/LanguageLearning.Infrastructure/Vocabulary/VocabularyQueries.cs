using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed class VocabularyQueries(LexiGoDbContext database, TimeProvider clock)
{
    public async Task<WordPage> ListAsync(Guid userId, bool savedOnly, Guid? courseId, string? search,
        string status, int page, CancellationToken cancellationToken, Guid? collectionId = null, string? topic = null)
    {
        var now = clock.GetUtcNow();
        var recordings = ReviewedAudio.Ready(database.MediaAssets);
        var query = from word in database.VocabularyItems.AsNoTracking()
                    join course in database.Courses on word.CourseId equals course.Id
                    join language in database.Languages on course.TargetLanguageId equals language.Id
                    join saved in database.SavedWords.Where(item => item.UserId == userId) on word.Id equals saved.VocabularyItemId into personal
                    from saved in personal.DefaultIfEmpty()
                    where savedOnly ? saved != null : course.IsPublished
                    select new
                    {
                        word.Id,
                        CourseId = course.Id,
                        CourseTitle = course.Title,
                        LanguageCode = language.Code,
                        word.Word,
                        word.EnglishTranslation,
                        word.Example,
                        word.PartOfSpeech,
                        word.Topic,
                        HasAudio = course.IsPublished && recordings
                            .Any(media => media.Id == word.AudioMediaId && media.LanguageId == course.TargetLanguageId),
                        SavedWordId = saved == null ? (Guid?)null : saved.Id,
                        DueAt = saved == null ? (DateTimeOffset?)null : saved.DueAt,
                        IntervalDays = saved == null ? (int?)null : saved.IntervalDays
                    };
        if (courseId is { } id) query = query.Where(item => item.CourseId == id);
        if (!string.IsNullOrWhiteSpace(topic)) query = query.Where(item => item.Topic == topic);
        if (savedOnly && collectionId is { } collection)
            query = query.Where(item => database.CollectionWords.Any(link => link.CollectionId == collection &&
                link.SavedWordId == item.SavedWordId &&
                database.WordCollections.Any(owner => owner.Id == collection && owner.UserId == userId)));
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Search input is literal text, including SQL wildcard characters.
            string pattern = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(item => EF.Functions.ILike(item.Word, pattern, "\\") || EF.Functions.ILike(item.EnglishTranslation, pattern, "\\"));
        }
        if (status == "due") query = query.Where(item => item.DueAt <= now);
        if (status == "scheduled") query = query.Where(item => item.DueAt > now);
        int count = await query.CountAsync(cancellationToken);
        var ordered = savedOnly ? query.OrderBy(item => item.DueAt).ThenBy(item => item.Id) : query.OrderBy(item => item.Word).ThenBy(item => item.Id);
        var items = await ordered.Skip((page - 1) * 20).Take(20)
            .Select(item => new WordView(item.Id, item.CourseId, item.CourseTitle, item.LanguageCode, item.Word,
                item.EnglishTranslation, item.Example, item.PartOfSpeech, item.SavedWordId, item.DueAt,
                item.IntervalDays, item.Topic, item.HasAudio))
            .ToArrayAsync(cancellationToken);
        return new(items, page, 20, count);
    }
}
