using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class LearningAudioService(LexiGoDbContext database, LearningAccess access,
    Lazy<MediaObjectStore> storage, IOptions<ObjectStorageOptions> options, HeartBalanceService hearts)
{
    public async Task<LearningOutcome> PlaybackAsync(Guid userId, Guid familyId, Guid sessionId, Guid exerciseId, CancellationToken cancellationToken, Guid? optionId = null)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var session = await database.LearningSessions.SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        var exercise = await database.SessionExercises.SingleOrDefaultAsync(item => item.Id == exerciseId && item.SessionId == sessionId && item.UserId == userId, cancellationToken);
        if (session is null || exercise is null) return new(Missing: true);
        if (session.CompletedAt is null && session.Mode != "Practice")
            session.Hearts = (await hearts.StatusAsync(userId, cancellationToken)).Count;
        Guid? referenceId = optionId is { } selectedOption
            ? await database.SessionOptionAudio.Where(item => item.SessionExerciseId == exerciseId && item.OptionId == selectedOption)
                .Select(item => (Guid?)item.AudioMediaId).SingleOrDefaultAsync(cancellationToken)
            : exercise.AudioMediaId;
        if (referenceId is not { } mediaId) return new(Missing: true);
        bool answered = await database.AnswerAttempts.AnyAsync(item => item.UserId == userId && item.SessionExerciseId == exerciseId, cancellationToken);
        if (!answered)
        {
            if (session.CompletedAt is not null || session.Mode != "Practice" && session.Hearts == 0) return new(Missing: true);
            var currentId = await database.SessionExercises.Where(item => item.SessionId == sessionId
                && !database.AnswerAttempts.Any(answer => answer.UserId == userId && answer.SessionExerciseId == item.Id))
                .OrderBy(item => item.OrderNumber).Select(item => item.Id).FirstOrDefaultAsync(cancellationToken);
            if (exerciseId != currentId) return new(Missing: true);
        }
        int languageId = await database.Courses.Where(item => item.Id == session.CourseId).Select(item => item.TargetLanguageId).SingleAsync(cancellationToken);
        var media = await ReviewedAudio.ForLanguage(database.MediaAssets, languageId).SingleOrDefaultAsync(item => item.Id == mediaId, cancellationToken);
        if (media is null) return new(Missing: true);
        if (!options.Value.Enabled) return new(Unavailable: true);
        string url = await storage.Value.CreateReadLinkAsync(media.StorageKey, TimeSpan.FromMinutes(5));
        return new(new AudioPlaybackLink(url, 300));
    }
}
