using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed record StaffWord(Guid Id, string Word, string EnglishTranslation, Guid? AudioMediaId);
public sealed record StaffWordPage(StaffWord[] Items, int Total);

public sealed class StaffVocabularyAudio(LexiGoDbContext database, ContentAccess access,
    ContentTransactions content, TimeProvider clock)
{
    private const int PageSize = 20;

    public async Task<StaffWordPage?> ListAsync(Guid actorId, Guid familyId, Guid courseId,
        int page, CancellationToken cancellationToken)
    {
        if (await access.ActorAsync(actorId, familyId, false, cancellationToken) is not { IsAdmin: true })
            return null;

        var words = database.VocabularyItems.AsNoTracking().Where(word => word.CourseId == courseId);
        int total = await words.CountAsync(cancellationToken);
        var items = await words.OrderBy(word => word.Word).ThenBy(word => word.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(word => new StaffWord(word.Id, word.Word, word.EnglishTranslation, word.AudioMediaId))
            .ToArrayAsync(cancellationToken);
        return new(items, total);
    }

    public async Task<ContentOutcome> AttachAsync(Guid actorId, Guid familyId, Guid wordId,
        Guid? audioId, Guid? previousAudioId, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await access.ActorAsync(actorId, familyId, true, cancellationToken) is not { IsAdmin: true })
            return new(Forbidden: true);

        Guid? courseId = await database.VocabularyItems.Where(word => word.Id == wordId)
            .Select(word => (Guid?)word.CourseId).SingleOrDefaultAsync(cancellationToken);
        if (courseId is null) return new(Error: "This vocabulary word no longer exists.");
        var course = await content.LockCourseAsync(courseId.Value, cancellationToken);
        var entry = await database.VocabularyItems.SingleOrDefaultAsync(word => word.Id == wordId, cancellationToken);
        if (course is null || entry is null) return new(Error: "This vocabulary word no longer exists.");

        if (entry.AudioMediaId == audioId) return new(Id: wordId);
        if (entry.AudioMediaId != previousAudioId)
            return new(Error: "The recording changed. Reload the word list and try again.");
        if (audioId is { } recordingId)
        {
            var media = await ReviewedAudio.LockAsync(database, [recordingId], cancellationToken);
            if (!ReviewedAudio.ForLanguage(media.AsQueryable(), course.TargetLanguageId).Any())
                return new(Error: "Choose an independently reviewed recording in the course language.");
        }

        entry.AudioMediaId = audioId;
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            EntityType = "VocabularyItem",
            EntityId = wordId.ToString(),
            Action = audioId is null ? "VocabularyAudioDetached" : "VocabularyAudioAttached",
            Reason = audioId is null ? "Pronunciation recording removed." : "Reviewed pronunciation recording attached.",
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Id: wordId);
    }
}
