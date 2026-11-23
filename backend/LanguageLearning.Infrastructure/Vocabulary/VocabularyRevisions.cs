using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed class VocabularyRevisions(LexiGoDbContext database, ContentAccess access,
    ContentTransactions content, TimeProvider clock)
{
    public async Task<VocabularyRevisionView?> GetAsync(Guid actorId, Guid familyId, Guid id, CancellationToken token)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, token);
        if (actor is null) return null;
        var context = await (from revision in database.VocabularyRevisions.AsNoTracking()
                             join course in database.Courses on revision.CourseId equals course.Id
                             where revision.Id == id
                             select new { Revision = revision, course.TargetLanguageId }).SingleOrDefaultAsync(token);
        if (context is null) return null;
        var row = context.Revision;
        bool author = actor.CanAuthor(context.TargetLanguageId) && row.AuthorId == actor.Id;
        bool reviewer = actor.Roles.Contains(RoleNames.Reviewer) && actor.LanguageIds.Contains(context.TargetLanguageId)
            && row.Status is "Submitted" or "Approved" or "Published";
        if (!actor.IsAdmin && !author && !reviewer) return null;
        return new(row.Id, row.RevisionToken, row.WordId, row.AuthorId, row.ReviewerId, row.PublisherId,
            row.Status, new VocabularyText(row.Word, row.EnglishTranslation, row.Example, row.PartOfSpeech, row.Topic),
            row.ReviewComment, row.CreatedAt, row.ReviewedAt, row.PublishedAt);
    }

    public async Task<VocabularyRevisionView[]?> ListAsync(Guid actorId, Guid familyId, Guid courseId,
        int page, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        int? language = await database.Courses.Where(course => course.Id == courseId)
            .Select(course => (int?)course.TargetLanguageId).SingleOrDefaultAsync(cancellationToken);
        if (actor is null || language is null) return null;
        bool reviewer = actor.Roles.Contains(RoleNames.Reviewer) && actor.LanguageIds.Contains(language.Value);
        bool author = actor.CanAuthor(language.Value);
        if (!author && !reviewer) return null;

        return await database.VocabularyRevisions.AsNoTracking()
            .Where(item => item.CourseId == courseId && (actor.IsAdmin ||
                author && item.AuthorId == actor.Id ||
                reviewer && (item.Status == "Submitted" || item.Status == "Approved" || item.Status == "Published")))
            .OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .Skip((page - 1) * 20).Take(20)
            .Select(item => new VocabularyRevisionView(item.Id, item.RevisionToken, item.WordId,
                item.AuthorId, item.ReviewerId, item.PublisherId, item.Status,
                new VocabularyText(item.Word, item.EnglishTranslation, item.Example, item.PartOfSpeech, item.Topic),
                item.ReviewComment, item.CreatedAt, item.ReviewedAt, item.PublishedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ContentOutcome> CreateAsync(Guid actorId, Guid familyId, Guid courseId,
        Guid? wordId, VocabularyText text, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        var course = actor is null ? null : await content.LockCourseAsync(courseId, cancellationToken);
        if (actor is null || course is null || !actor.CanAuthor(course.TargetLanguageId)) return new(Forbidden: true);

        if (wordId is { } id)
        {
            if (!await database.VocabularyItems.AnyAsync(item => item.Id == id && item.CourseId == courseId, cancellationToken))
                return new(Error: "Choose a word from this course.");
            if (await database.VocabularyRevisions.AnyAsync(item => item.WordId == id &&
                item.Status != "Published" && item.Status != "Withdrawn", cancellationToken))
                return new(Error: "This word already has an open revision.");
        }
        var revision = new VocabularyRevision { CourseId = courseId, WordId = wordId, AuthorId = actorId, CreatedAt = clock.GetUtcNow() };
        SetText(revision, text);
        database.VocabularyRevisions.Add(revision);
        Audit(revision, actorId, "VocabularyDraftCreated", "Vocabulary draft created.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(revision.Id);
    }

    public async Task<ContentOutcome> ChangeAsync(Guid actorId, Guid familyId, Guid revisionId,
        Guid token, string action, VocabularyText? text, string? comment, string traceId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is null) return new(Forbidden: true);
        Guid? courseId = await database.VocabularyRevisions.Where(item => item.Id == revisionId)
            .Select(item => (Guid?)item.CourseId).SingleOrDefaultAsync(cancellationToken);
        if (courseId is null) return new(Forbidden: true);
        var course = await content.LockCourseAsync(courseId.Value, cancellationToken);
        var revision = await database.VocabularyRevisions.FindAsync([revisionId], cancellationToken);
        if (course is null || revision is null) return new(Forbidden: true);

        bool allowed = action switch
        {
            "edit" or "submit" => actor.Id == revision.AuthorId && actor.CanAuthor(course.TargetLanguageId),
            "withdraw" => actor.IsAdmin || actor.Id == revision.AuthorId && actor.CanAuthor(course.TargetLanguageId),
            "approve" or "request-changes" => actor.CanReview(course.TargetLanguageId, revision.AuthorId, revision.AuthorId),
            "publish" => actor.IsAdmin,
            _ => false
        };
        if (!allowed) return new(Forbidden: true);
        if (action == "publish" && revision.Status == "Published") return new(revisionId);
        if (action == "withdraw" && revision.Status == "Withdrawn") return new(revisionId);
        if (revision.RevisionToken != token) return new(Error: "The revision changed. Reload it and try again.");

        switch (action)
        {
            case "withdraw" when revision.Status != "Published":
                revision.Status = "Withdrawn";
                break;
            case "edit" when revision.Status == "Draft" && text is not null:
                SetText(revision, text);
                break;
            case "submit" when revision.Status == "Draft":
                revision.Status = "Submitted";
                break;
            case "approve" or "request-changes" when revision.Status == "Submitted" && !string.IsNullOrWhiteSpace(comment):
                revision.ReviewerId = actorId;
                revision.ReviewedAt = clock.GetUtcNow();
                revision.ReviewComment = comment.Trim();
                revision.Status = action == "approve" ? "Approved" : "Draft";
                break;
            case "publish" when revision.Status == "Approved":
                await PublishAsync(revision, actorId, cancellationToken);
                break;
            default:
                return new(Error: "This action is unavailable in the current review state.");
        }
        revision.RevisionToken = Guid.NewGuid();
        Audit(revision, actorId, $"VocabularyRevision:{action}", comment?.Trim() ?? "Vocabulary workflow updated.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(revisionId);
    }

    private async Task PublishAsync(VocabularyRevision revision, Guid actorId, CancellationToken cancellationToken)
    {
        var word = revision.WordId is { } id
            ? await database.VocabularyItems.SingleAsync(item => item.Id == id && item.CourseId == revision.CourseId, cancellationToken)
            : new VocabularyItem { CourseId = revision.CourseId };
        if (revision.WordId is null) database.VocabularyItems.Add(word);
        if (word.Word != revision.Word) word.AudioMediaId = null;
        word.Word = revision.Word;
        word.EnglishTranslation = revision.EnglishTranslation;
        word.Example = revision.Example;
        word.PartOfSpeech = revision.PartOfSpeech;
        word.Topic = revision.Topic;
        revision.WordId = word.Id;
        revision.Status = "Published";
        revision.PublisherId = actorId;
        revision.PublishedAt = clock.GetUtcNow();
    }

    private static void SetText(VocabularyRevision revision, VocabularyText text)
    {
        revision.Word = text.Word.Trim();
        revision.EnglishTranslation = text.EnglishTranslation.Trim();
        revision.Example = text.Example?.Trim() ?? "";
        revision.PartOfSpeech = text.PartOfSpeech?.Trim() ?? "";
        revision.Topic = string.IsNullOrEmpty(text.Topic) ? null : text.Topic;
    }

    private void Audit(VocabularyRevision revision, Guid actorId, string action, string reason, string traceId) =>
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            EntityType = "VocabularyRevision",
            EntityId = revision.Id.ToString(),
            Action = action,
            Reason = reason,
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });
}
