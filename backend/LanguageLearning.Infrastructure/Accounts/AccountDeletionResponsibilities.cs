using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed record DeletionResponsibility(string Code, string Message);

public sealed class AccountDeletionResponsibilities(LexiGoDbContext database, LearningAccess access)
{
    public async Task<DeletionResponsibility[]?> ForSessionAsync(Guid userId, Guid familyId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return null;
        return await InspectAsync(userId, cancellationToken);
    }

    // Deletion calls this again inside its own user-locked transaction.
    internal async Task<DeletionResponsibility[]> InspectAsync(Guid userId, CancellationToken cancellationToken)
    {
        var pending = new List<DeletionResponsibility>();
        if (await database.Dialogues.AnyAsync(item => item.OwnerId == userId
            && item.Status != "Published" && item.Status != "Archived", cancellationToken))
            pending.Add(new("dialogue-drafts", "Publish or archive your unfinished dialogue revisions before deleting your account."));
        if (await database.Classrooms.AnyAsync(item => item.TeacherId == userId && !item.IsArchived, cancellationToken))
            pending.Add(new("classes", "Hand over or archive your active classes before deleting your account."));
        if (await database.ExtraStudyOffers.AnyAsync(item => item.TeacherId == userId && item.Enabled, cancellationToken)
            || await database.ExtraStudyRequests.AnyAsync(item => (item.Status == "Pending" || item.Status == "Accepted") &&
                database.ExtraStudyOffers.Any(offer => offer.Id == item.OfferId && offer.TeacherId == userId), cancellationToken))
            pending.Add(new("extra-study", "Disable your teacher listings and close outstanding Extra Study requests."));
        if (await database.LessonVersions.AnyAsync(item => item.CreatedBy == userId &&
                item.PrivateTextErasedAt == null && item.Status != "Published" &&
                (item.Status != "Archived" || item.PublishedAt == null), cancellationToken)
            || await database.Lessons.AnyAsync(lesson => lesson.OwnerId == userId && lesson.CurrentVersionId == null &&
                !database.LessonVersions.Any(version => version.LessonId == lesson.Id && version.PublishedAt != null) &&
                database.LessonVersions.Any(version => version.LessonId == lesson.Id && version.PrivateTextErasedAt == null),
                cancellationToken))
            pending.Add(new("lesson-drafts", "Resolve your unpublished content revisions. Shared unfinished lessons require staff assistance."));
        if (await database.VocabularyRevisions.AnyAsync(item => item.AuthorId == userId &&
                item.Status != "Published" && item.Status != "Withdrawn", cancellationToken))
            pending.Add(new("vocabulary-drafts", "Publish or withdraw your unpublished vocabulary revisions."));
        if (await database.MediaAssets.AnyAsync(media => media.UploadedBy == userId
            && (media.MimeType != "image/png" || media.Source != ProfileAvatarService.MediaSource)
            && !database.Exercises.Any(item => item.AudioMediaId == media.Id)
            && !database.ExerciseOptions.Any(item => item.AudioMediaId == media.Id)
            && !database.SessionExercises.Any(item => item.AudioMediaId == media.Id)
            && !database.SessionOptionAudio.Any(item => item.AudioMediaId == media.Id)
            && !database.DialogueNodes.Any(item => item.AudioMediaId == media.Id)
            && !database.VocabularyItems.Any(item => item.AudioMediaId == media.Id), cancellationToken))
            pending.Add(new("recordings", "Remove your unused uploaded recordings before deleting your account."));
        return pending.ToArray();
    }
}
