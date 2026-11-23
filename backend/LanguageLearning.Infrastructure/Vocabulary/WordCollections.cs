using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed record WordCollectionView(Guid Id, Guid CourseId, string Name, int WordCount);
public sealed record WordCollectionOutcome(Guid? Id = null, string? Error = null, bool Created = false);

public sealed class WordCollections(LexiGoDbContext database, LearningAccess access)
{
    public Task<WordCollectionView[]> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        Views(database.WordCollections.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.Name).ThenBy(item => item.Id).Take(100)).ToArrayAsync(cancellationToken);

    public Task<WordCollectionView?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        Views(database.WordCollections.AsNoTracking().Where(item => item.UserId == userId && item.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<WordCollectionView> Views(IQueryable<WordCollection> items) =>
        items.Select(item => new WordCollectionView(item.Id, item.CourseId, item.Name,
            database.CollectionWords.Count(word => word.CollectionId == item.Id)));

    public async Task<WordCollectionOutcome> SaveAsync(
        Guid userId, Guid familyId, Guid? id, Guid? courseId, string name, CancellationToken cancellationToken)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 80 || name.Any(char.IsControl))
            return new(Error: "Enter a collection name of 1 to 80 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new();
        var own = database.WordCollections.Where(item => item.UserId == userId);
        var collection = id is { } existingId
            ? await own.SingleOrDefaultAsync(item => item.Id == existingId, cancellationToken)
            : null;
        if (id.HasValue && collection is null) return new();
        var selectedCourse = collection?.CourseId ?? courseId;
        if (selectedCourse is not { } chosen) return new();
        if (collection is null && !await database.Courses.AnyAsync(item => item.Id == chosen && item.IsPublished, cancellationToken))
            return new();
        var sameName = await own.SingleOrDefaultAsync(
            item => item.CourseId == chosen && item.Name == name, cancellationToken);
        if (sameName is not null && sameName.Id != id)
            return id is null ? new(sameName.Id) : new(Error: "A collection already uses that name in this course.");
        if (collection is null)
        {
            if (await own.CountAsync(cancellationToken) >= 100) return new(Error: "You already have 100 collections.");
            collection = new WordCollection { UserId = userId, CourseId = chosen, Name = name };
            database.WordCollections.Add(collection);
        }
        else collection.Name = name;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(collection.Id, Created: id is null);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid familyId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return false;
        var affected = await database.WordCollections.Where(item => item.UserId == userId && item.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affected != 0;
    }

    public async Task<bool> SetWordAsync(
        Guid userId, Guid familyId, Guid id, Guid savedWordId, bool add, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return false;
        var allowed = await (
            from collection in database.WordCollections
            join word in database.VocabularyItems on collection.CourseId equals word.CourseId
            join saved in database.SavedWords on word.Id equals saved.VocabularyItemId
            where collection.Id == id && collection.UserId == userId && saved.Id == savedWordId && saved.UserId == userId
            select collection.Id).AnyAsync(cancellationToken);
        if (!allowed) return false;
        var link = await database.CollectionWords.FindAsync([id, savedWordId], cancellationToken);
        if (add && link is null)
            database.CollectionWords.Add(new CollectionWord { CollectionId = id, SavedWordId = savedWordId });
        else if (!add && link is not null)
            database.CollectionWords.Remove(link);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
