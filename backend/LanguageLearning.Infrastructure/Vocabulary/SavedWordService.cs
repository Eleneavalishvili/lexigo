using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed record SavedWordOutcome(SaveWordResult? Word = null, string? Error = null, bool Created = false);

public sealed class SavedWordService(LexiGoDbContext database, LearningAccess access, TimeProvider clock, AchievementAwards achievements)
{
    public Task<SaveWordResult?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        database.SavedWords.AsNoTracking().Where(item => item.UserId == userId && item.Id == id)
            .Select(item => new SaveWordResult(item.Id, item.VocabularyItemId, item.DueAt, item.IntervalDays, item.CorrectStreak))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SavedWordOutcome> SaveAsync(Guid userId, Guid familyId, Guid wordId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new();
        var saved = await database.SavedWords.SingleOrDefaultAsync(item => item.UserId == userId && item.VocabularyItemId == wordId, cancellationToken);
        bool created = saved is null;
        if (saved is null)
        {
            if (!await (from word in database.VocabularyItems
                        join course in database.Courses on word.CourseId equals course.Id
                        where word.Id == wordId && course.IsPublished
                        select word.Id).AnyAsync(cancellationToken)) return new();
            if (await database.SavedWords.CountAsync(item => item.UserId == userId, cancellationToken) >= 10000)
                return new(Error: "Your library is full. Remove a word before saving another.");
            saved = new SavedWord { UserId = userId, VocabularyItemId = wordId, DueAt = clock.GetUtcNow() };
            database.SavedWords.Add(saved);
            await database.SaveChangesAsync(cancellationToken);
            // Return the stored timestamp precision, so concurrent retries receive the same schedule.
            await database.Entry(saved).ReloadAsync(cancellationToken);
        }
        await achievements.AwardLockedAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(new SaveWordResult(saved.Id, saved.VocabularyItemId, saved.DueAt, saved.IntervalDays, saved.CorrectStreak),
            Created: created);
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid familyId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return false;
        var saved = await database.SavedWords.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
        if (saved is null) return false;
        database.SavedWords.Remove(saved);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
