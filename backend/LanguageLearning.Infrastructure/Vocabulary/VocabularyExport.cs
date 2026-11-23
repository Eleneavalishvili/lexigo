using System.Data;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Accounts.ExportJson;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed class VocabularyExport(LexiGoDbContext database, TimeProvider clock)
{
    public async Task WriteAsync(Guid userId, Stream destination, CancellationToken cancellationToken)
    {
        // All sections reflect the same point in time, even if the library changes during download.
        await using var snapshot = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await StartAsync(destination, clock.GetUtcNow(), cancellationToken);
        await WriteSectionsAsync(userId, destination, cancellationToken);
        await snapshot.CommitAsync(cancellationToken);
        await destination.WriteAsync("}"u8.ToArray(), cancellationToken);
    }

    internal async Task WriteSectionsAsync(Guid userId, Stream destination, CancellationToken cancellationToken)
    {
        var saved = database.SavedWords.AsNoTracking().Where(item => item.UserId == userId);
        var collections = database.WordCollections.AsNoTracking().Where(item => item.UserId == userId);

        var words = from item in saved
                    join word in database.VocabularyItems on item.VocabularyItemId equals word.Id
                    orderby item.Id
                    select new
                    {
                        item.Id, word.CourseId, item.VocabularyItemId, word.Word, word.EnglishTranslation,
                        word.Example, word.PartOfSpeech, word.Topic, item.DueAt, item.IntervalDays, item.CorrectStreak
                    };
        var reviews = from review in database.WordReviews.AsNoTracking()
                      join item in saved on review.SavedWordId equals item.Id
                      orderby review.ReviewedAt, review.Id
                      select new { review.SavedWordId, review.IsCorrect, review.ReviewedAt, review.DueAt, review.IntervalDays };
        var memberships = from membership in database.CollectionWords.AsNoTracking()
                          join collection in collections on membership.CollectionId equals collection.Id
                          join item in saved on membership.SavedWordId equals item.Id
                          orderby membership.CollectionId, membership.SavedWordId
                          select new { membership.CollectionId, membership.SavedWordId };

        // Finish serializing each section so its reader closes before the next query starts.
        await WriteSectionAsync(destination, ",\"savedWords\":"u8.ToArray(), words, cancellationToken);
        await WriteSectionAsync(destination, ",\"reviews\":"u8.ToArray(), reviews, cancellationToken);
        await WriteSectionAsync(destination, ",\"collections\":"u8.ToArray(), collections.OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.CourseId, item.Name }), cancellationToken);
        await WriteSectionAsync(destination, ",\"collectionWords\":"u8.ToArray(), memberships, cancellationToken);
    }
}
