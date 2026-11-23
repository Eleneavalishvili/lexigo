using LanguageLearning.Core.Content;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class LessonWorkflow(LexiGoDbContext database, ContentTransactions content, LessonValidation validation, TimeProvider clock)
{
    public async Task<ContentOutcome> TransitionAsync(Guid actorId, Guid familyId, Guid lessonId, Guid versionId,
        string operation, string comment, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var state = await content.LessonAsync(actorId, familyId, lessonId, cancellationToken);
        if (state is not { } context) return new(Forbidden: true);
        var (actor, lesson, course, version) = context;
        if (version.Id != versionId) return new(Error: "The version has changed. Reload the lesson.");
        switch (operation)
        {
            case "submit":
                if (!actor.CanEdit(course.TargetLanguageId, lesson.OwnerId)) return new(Forbidden: true);
                if (version.Status != "Draft") return new(Error: "Only a draft can be submitted.");
                string? error = await validation.ValidateAsync(version.Id, cancellationToken);
                if (error is not null) return new(Error: error);
                version.Status = "Submitted";
                if (lesson.CurrentVersionId is null) lesson.Status = "Submitted";
                break;
            case "Approved":
            case "ChangesRequested":
                if (!actor.CanReview(course.TargetLanguageId, lesson.OwnerId, version.CreatedBy)) return new(Forbidden: true);
                if (version.Status != "Submitted" || string.IsNullOrWhiteSpace(comment) || comment.Length > 3000) return new(Error: "Review a submitted version and provide a comment.");
                database.ContentReviews.Add(new ContentReview { LessonVersionId = version.Id, ReviewerId = actorId, Decision = operation, Comment = comment, ReviewedAt = clock.GetUtcNow() });
                version.Status = operation == "Approved" ? "Approved" : "Draft";
                if (lesson.CurrentVersionId is null) lesson.Status = version.Status;
                break;
            case "publish":
                if (!actor.IsAdmin) return new(Forbidden: true);
                if (course.ArchivedAt.HasValue) return new(Error: "Restore the course before publishing lessons.");
                if (await database.Units.AnyAsync(unit => unit.Id == lesson.UnitId && unit.ArchivedAt != null, cancellationToken))
                    return new(Error: "Restore the unit before publishing lessons.");
                if (version.Status == "Published" && lesson.CurrentVersionId == version.Id && lesson.Status == "Published") return new(lessonId);
                if (version.Status != "Approved" || !await database.ContentReviews.AnyAsync(item => item.LessonVersionId == version.Id
                    && item.Decision == "Approved" && item.ReviewerId != version.CreatedBy && item.ReviewerId != lesson.OwnerId, cancellationToken)) return new(Error: "Independent approval is required before publication.");
                string? publicationError = await validation.ValidateAsync(version.Id, cancellationToken);
                if (publicationError is not null) return new(Error: publicationError);
                var prerequisites = await database.LessonPrerequisites.Where(item => item.LessonId == lessonId).Select(item => item.PrerequisiteLessonId).ToArrayAsync(cancellationToken);
                if (await database.Lessons.CountAsync(item => prerequisites.Contains(item.Id) && item.Status == "Published" && item.CurrentVersionId != null
                    && database.Units.Any(unit => unit.Id == item.UnitId && unit.ArchivedAt == null), cancellationToken) != prerequisites.Length)
                    return new(Error: "Publish prerequisite lessons in active units first.");
                if (lesson.CurrentVersionId is { } previousId)
                    (await database.LessonVersions.SingleAsync(item => item.Id == previousId, cancellationToken)).Status = "Archived";
                version.Status = "Published"; version.PublishedAt = clock.GetUtcNow();
                lesson.Status = "Published"; lesson.CurrentVersionId = version.Id; course.IsPublished = true;
                break;
            case "archive":
                if (lesson.CurrentVersionId is not null ? !actor.IsAdmin : !actor.CanEdit(course.TargetLanguageId, lesson.OwnerId)) return new(Forbidden: true);
                if (await database.LessonPrerequisites.AnyAsync(edge => edge.PrerequisiteLessonId == lessonId && database.Lessons.Any(child => child.Id == edge.LessonId && child.Status == "Published"), cancellationToken)) return new(Error: "Archive dependent published lessons first.");
                lesson.Status = "Archived";
                var open = await database.LessonVersions.Where(item => item.LessonId == lessonId && item.Status != "Archived").ToArrayAsync(cancellationToken);
                foreach (var item in open) item.Status = "Archived";
                course.IsPublished = !course.ArchivedAt.HasValue && await database.Lessons.AnyAsync(other => other.Id != lessonId && other.Status == "Published" && other.CurrentVersionId != null
                    && database.Units.Any(unit => unit.Id == other.UnitId && unit.CourseId == course.Id && unit.ArchivedAt == null), cancellationToken);
                break;
            default: return new(Error: "Unknown workflow operation.");
        }
        string action = operation switch { "submit" => "LessonSubmitted", "Approved" => "LessonReviewApproved", "ChangesRequested" => "LessonChangesRequested", "publish" => "LessonPublished", _ => "LessonArchived" };
        content.Audit(actorId, lessonId, action, comment, traceId);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(lessonId);
    }

    public async Task<ContentOutcome> RevisionAsync(Guid actorId, Guid familyId, Guid lessonId, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var state = await content.LessonAsync(actorId, familyId, lessonId, cancellationToken);
        if (state is not { } context || !context.Actor.CanEdit(context.Course.TargetLanguageId, context.Lesson.OwnerId)) return new(Forbidden: true);
        var previous = context.Version;
        if (previous.Status is not ("Published" or "Archived")) return new(Error: "Finish the current draft/review before starting another version.");
        if (previous.PrivateTextErasedAt is not null)
        {
            var published = await database.LessonVersions.Where(item => item.LessonId == lessonId && item.PublishedAt != null)
                .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
            if (published is null) return new(Error: "Create a new lesson after erasing its private drafts.");
            previous = published;
        }
        var version = new LessonVersion
        {
            LessonId = lessonId,
            CreatedBy = actorId,
            VersionNumber = context.Version.VersionNumber + 1,
            Title = previous.Title,
            Objective = previous.Objective,
            Explanation = previous.Explanation
        };
        var error = await CopyExercisesAsync(previous.Id, version.Id, cancellationToken);
        if (error is not null) return new(Error: error);
        database.LessonVersions.Add(version);
        if (context.Lesson.CurrentVersionId is null) context.Lesson.Status = "Draft";
        content.Audit(actorId, lessonId, "LessonRevisionCreated", "A new draft preserves the earlier version and its exercises.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(version.Id);
    }

    public async Task<ContentOutcome> DuplicateAsync(Guid actorId, Guid familyId, Guid lessonId, Guid versionId,
        string title, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var state = await content.LessonAsync(actorId, familyId, lessonId, cancellationToken);
        if (state is not { } source || !source.Actor.CanEdit(source.Course.TargetLanguageId, source.Lesson.OwnerId))
            return new(Forbidden: true);
        if (source.Version.Id != versionId || source.Version.Status != "Draft" || source.Version.PrivateTextErasedAt is not null)
            return new(Error: "Select the current draft to duplicate it.");
        title = title.Trim();
        if (title.Length == 0) return new(Error: "Enter a title for the new lesson.");

        var lastPosition = await database.Lessons.Where(item => item.UnitId == source.Lesson.UnitId)
            .MaxAsync(item => item.OrderNumber, cancellationToken);
        if (lastPosition >= 10000) return new(Error: "There is no available position at the end of this unit.");
        var lesson = new Lesson
        {
            UnitId = source.Lesson.UnitId, OwnerId = actorId, Title = title,
            Objective = source.Version.Objective, OrderNumber = lastPosition + 1
        };
        var version = new LessonVersion
        {
            LessonId = lesson.Id, VersionNumber = 1, CreatedBy = actorId, Title = title,
            Objective = source.Version.Objective, Explanation = source.Version.Explanation
        };
        var error = await CopyExercisesAsync(source.Version.Id, version.Id, cancellationToken);
        if (error is not null) return new(Error: error);
        database.Lessons.Add(lesson);
        database.LessonVersions.Add(version);
        content.Audit(actorId, lesson.Id, "LessonDraftDuplicated", $"Copied from lesson {lessonId}.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(lesson.Id);
    }

    private async Task<string?> CopyExercisesAsync(Guid sourceVersionId, Guid targetVersionId, CancellationToken cancellationToken)
    {
        var exercises = await database.Exercises.Where(item => item.LessonVersionId == sourceVersionId).ToArrayAsync(cancellationToken);
        var ids = exercises.Select(item => item.Id).ToArray();
        var skillTags = (await database.ExerciseSkills.Where(item => ids.Contains(item.ExerciseId))
            .ToArrayAsync(cancellationToken)).ToLookup(item => item.ExerciseId);
        var options = await database.ExerciseOptions.Where(item => ids.Contains(item.ExerciseId)).ToArrayAsync(cancellationToken);
        var answers = await database.AcceptedAnswers.Where(item => ids.Contains(item.ExerciseId)).ToArrayAsync(cancellationToken);
        foreach (var old in exercises)
        {
            if (!ExerciseFormats.SupportsLearning(old.Type)) return "This exercise format cannot be copied.";
            var copy = new Exercise
            {
                LessonVersionId = targetVersionId,
                Type = old.Type,
                Prompt = old.Prompt,
                ContextText = old.ContextText,
                Explanation = old.Explanation,
                OrderNumber = old.OrderNumber,
                AudioMediaId = old.AudioMediaId,
                EvaluationRules = old.EvaluationRules
            };
            database.Exercises.Add(copy);
            database.ExerciseSkills.AddRange(skillTags[old.Id].Select(tag =>
                new ExerciseSkill { ExerciseId = copy.Id, SkillId = tag.SkillId }));
            var oldChoices = options.Where(item => item.ExerciseId == old.Id).ToArray();
            if (old.Type == "Matching" && MatchingAnswerKeys.Read(oldChoices) is null) return "Repair the matching pairs before copying them.";
            var pairKeys = oldChoices.Where(item => item.MatchKey != null).Select(item => item.MatchKey!).Distinct().ToDictionary(key => key, _ => Guid.NewGuid().ToString("N"));
            var newChoices = oldChoices.ToDictionary(item => item.Id, item => new ExerciseOption
            {
                ExerciseId = copy.Id,
                Text = item.Text,
                OrderNumber = item.OrderNumber,
                AudioMediaId = item.AudioMediaId,
                MatchKey = old.Type == "Matching" ? pairKeys[item.MatchKey!] : item.MatchKey
            });
            database.ExerciseOptions.AddRange(newChoices.Values);
            foreach (var answer in answers.Where(item => item.ExerciseId == old.Id))
            {
                string? tokenIds = null;
                if (old.Type == "SentenceBuilder")
                {
                    var order = SentenceAnswerKeys.Read(answer.OrderedTokenIds);
                    if (order is null || !SentenceAnswerKeys.IsSelection(order, oldChoices.Select(item => item.Id).ToArray())) return "Repair the previous token answer before copying it.";
                    tokenIds = System.Text.Json.JsonSerializer.Serialize(order.Select(id => newChoices[id].Id).ToArray());
                }
                database.AcceptedAnswers.Add(new AcceptedAnswer { ExerciseId = copy.Id, AnswerText = answer.AnswerText, OrderedTokenIds = tokenIds });
            }
        }
        return null;
    }
}
