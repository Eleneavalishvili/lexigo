using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Accounts.ExportJson;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class AuthoredContentExport(LexiGoDbContext database)
{
    internal async Task WriteSectionsAsync(Guid userId, Stream destination, CancellationToken cancellationToken)
    {
        var lessons = database.Lessons.AsNoTracking().Where(item => item.OwnerId == userId)
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.UnitId, item.Title, item.Objective, item.Status });
        var versions = database.LessonVersions.AsNoTracking().Where(item => item.CreatedBy == userId)
            .OrderBy(item => item.LessonId).ThenBy(item => item.VersionNumber)
            .Select(item => new
            {
                item.Id, item.LessonId, item.VersionNumber, item.Title, item.Objective,
                item.Explanation, item.Status, item.PublishedAt, item.PrivateTextErasedAt
            });
        var reviews = database.ContentReviews.AsNoTracking().Where(item => item.ReviewerId == userId)
            .OrderBy(item => item.ReviewedAt).ThenBy(item => item.Id)
            .Select(item => new { item.LessonVersionId, item.Decision, item.Comment, item.ReviewedAt });
        var media = database.MediaAssets.AsNoTracking().Where(item => item.UploadedBy == userId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new
            {
                item.Id, item.MimeType, item.ByteSize, item.LanguageId, item.Source, item.License,
                item.IsVerified, item.StorageState, item.CreatedAt, item.DurationMilliseconds
            });
        var vocabulary = database.VocabularyRevisions.AsNoTracking().Where(item => item.AuthorId == userId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new
            {
                item.Id, item.CourseId, item.WordId, item.Word, item.EnglishTranslation, item.Example,
                item.PartOfSpeech, item.Topic, item.Status, item.ReviewComment, item.CreatedAt,
                item.ReviewedAt, item.PublishedAt
            });
        var taughtClasses = database.Classrooms.AsNoTracking().Where(item => item.TeacherId == userId)
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.CourseId, item.Name, item.Description, item.IsArchived });
        var offers = database.ExtraStudyOffers.AsNoTracking().Where(item => item.TeacherId == userId)
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.CourseId, item.Enabled });
        var imports = database.ImportJobs.AsNoTracking().Where(item => item.AuthorId == userId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.Status, item.CreatedAt, item.LessonId });
        var actions = database.AuditLogs.AsNoTracking().Where(item => item.ActorId == userId)
            .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id)
            .Select(item => new { item.Action, item.EntityType, item.OccurredAt });

        await WriteSectionAsync(destination, ",\"authoredLessons\":"u8.ToArray(), lessons, cancellationToken);
        await WriteSectionAsync(destination, ",\"authoredLessonVersions\":"u8.ToArray(), versions, cancellationToken);
        await WriteSectionAsync(destination, ",\"lessonReviewsWritten\":"u8.ToArray(), reviews, cancellationToken);
        await WriteSectionAsync(destination, ",\"uploadedMedia\":"u8.ToArray(), media, cancellationToken);
        await WriteSectionAsync(destination, ",\"authoredVocabularyRevisions\":"u8.ToArray(), vocabulary, cancellationToken);
        await WriteSectionAsync(destination, ",\"taughtClasses\":"u8.ToArray(), taughtClasses, cancellationToken);
        await WriteSectionAsync(destination, ",\"extraStudyOffers\":"u8.ToArray(), offers, cancellationToken);
        await WriteSectionAsync(destination, ",\"contentImports\":"u8.ToArray(), imports, cancellationToken);
        await WriteSectionAsync(destination, ",\"administrativeActions\":"u8.ToArray(), actions, cancellationToken);
    }
}
