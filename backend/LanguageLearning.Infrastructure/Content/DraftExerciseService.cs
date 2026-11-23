using LanguageLearning.Core.Content;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class DraftExerciseService(LexiGoDbContext database, ContentTransactions content)
{
    public async Task<ContentOutcome> SaveAsync(Guid actorId, Guid familyId, Guid lessonId, Guid versionId, Guid? exerciseId,
        ExerciseInput input, string traceId, CancellationToken cancellationToken)
    {
        bool Text(string text, int length) => !string.IsNullOrWhiteSpace(text) && text.Length <= length && !text.Contains('\0');
        if (!ExerciseFormats.IsKnown(input.Type) || !Text(input.Prompt, 5000) || !Text(input.Explanation, 5000) || input.OrderNumber < 1
            || input.Options.Length > 20 || input.Answers.Length > 20 || input.Options.Any(value => !Text(value, 2000)) || input.Answers.Any(value => !Text(value, 5000))) return new(Error: "Check exercise fields and limits.");
        string? contextText = string.IsNullOrWhiteSpace(input.ContextText) ? null : input.ContextText.Trim();
        if (input.ContextText?.Length > 4000 || input.ContextText?.Contains('\0') == true
            || !ExerciseFormats.HasContext(input.Type) && contextText is not null)
            return new(Error: "Use context text up to 4000 characters for reading or dialogue choices only.");
        if (input.Type == "Speaking" && (input.Options.Length != 0 || input.Answers.Length != 0))
            return new(Error: "Speaking practice is ungraded and cannot contain choices or accepted answers.");
        int[][] tokenOrders = input.TokenOrders ?? [];
        if (input.Type == "SentenceBuilder")
        {
            if (input.Options.Length is < 2 or > 20 || input.Options.Any(token => token.Length > 80) || input.Answers.Length != 0
                || !SentenceAnswerKeys.ValidPositions(tokenOrders, input.Options.Length)) return new(Error: "Use 2–20 word tiles and distinct available numbers for each accepted order. Unused tiles are distractors.");
        }
        else if (tokenOrders.Length != 0) return new(Error: "Token orders are only used by sentence exercises.");
        var matchPairs = input.MatchPairs ?? [];
        if (input.Type == "Matching")
        {
            if (input.Options.Length != 0 || input.Answers.Length != 0 || !MatchingAnswerKeys.ValidInput(matchPairs))
                return new(Error: "Use 2–10 distinct matching pairs with no separate options or answers.");
        }
        else if (matchPairs.Length != 0) return new(Error: "Matching pairs are only used by matching exercises.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var state = await content.LessonAsync(actorId, familyId, lessonId, cancellationToken);
        if (state is not { } context || !context.Actor.CanEdit(context.Course.TargetLanguageId, context.Lesson.OwnerId)) return new(Forbidden: true);
        if (context.Version.Id != versionId || context.Version.Status != "Draft") return new(Error: "Only current draft exercises can be changed.");
        if (input.SkillIds is { } tags && (tags.Length > 10 || tags.Distinct().Count() != tags.Length
            || await database.Skills.CountAsync(item => item.CourseId == context.Course.Id
                && tags.Contains(item.Id) && (!item.IsArchived || exerciseId != null && database.ExerciseSkills.Any(
                    link => link.ExerciseId == exerciseId && link.SkillId == item.Id)), cancellationToken) != tags.Length))
            return new(Error: "Choose up to ten distinct skills from this course.");
        if (input.Type == "Matching" && input.AudioMediaId is not null) return new(Error: "Attach matching audio to individual left-side items.");
        var mediaIds = matchPairs.Where(pair => pair.LeftAudioMediaId.HasValue).Select(pair => pair.LeftAudioMediaId!.Value)
            .Concat(input.AudioMediaId is { } promptMedia ? new[] { promptMedia } : []).Distinct().Order().ToArray();
        var lockedMedia = await ReviewedAudio.LockAsync(database, mediaIds, cancellationToken);
        if (ReviewedAudio.ForLanguage(lockedMedia.AsQueryable(), context.Course.TargetLanguageId).Count() != mediaIds.Length
            || lockedMedia.Any(media => !context.Actor.CanEdit(context.Course.TargetLanguageId, media.UploadedBy)))
            return new(Error: "Choose your independently reviewed audio in this lesson's language.");
        if (input.Type == "Matching" && (lockedMedia.Any(media => media.DurationMilliseconds > 30000)
            || lockedMedia.Select(media => media.Sha256).Distinct().Count() != lockedMedia.Length))
            return new(Error: "Matching recordings must be distinct and no longer than 30 seconds.");
        var exercise = exerciseId.HasValue ? await database.Exercises.SingleOrDefaultAsync(item => item.Id == exerciseId && item.LessonVersionId == versionId, cancellationToken) : null;
        if (exerciseId.HasValue && exercise is null) return new(Forbidden: true);
        if (!exerciseId.HasValue && await database.Exercises.CountAsync(item => item.LessonVersionId == versionId, cancellationToken) >= 100) return new(Error: "This draft has reached 100 exercises.");
        if (await database.Exercises.AnyAsync(item => item.LessonVersionId == versionId && item.OrderNumber == input.OrderNumber && item.Id != exerciseId, cancellationToken)) return new(Error: "This exercise position is occupied.");
        if (exercise is null)
        {
            exercise = new Exercise { LessonVersionId = versionId, Type = input.Type, Prompt = input.Prompt, Explanation = input.Explanation, OrderNumber = input.OrderNumber };
            database.Exercises.Add(exercise);
        }
        else
        {
            database.ExerciseOptions.RemoveRange(await database.ExerciseOptions.Where(item => item.ExerciseId == exercise.Id).ToArrayAsync(cancellationToken));
            database.AcceptedAnswers.RemoveRange(await database.AcceptedAnswers.Where(item => item.ExerciseId == exercise.Id).ToArrayAsync(cancellationToken));
            await database.SaveChangesAsync(cancellationToken);
            exercise.Type = input.Type; exercise.Prompt = input.Prompt; exercise.Explanation = input.Explanation; exercise.OrderNumber = input.OrderNumber;
        }
        exercise.ContextText = contextText;
        if (input.SkillIds is { } skillIds)
        {
            var currentTags = await database.ExerciseSkills.Where(item => item.ExerciseId == exercise.Id)
                .ToArrayAsync(cancellationToken);
            database.ExerciseSkills.RemoveRange(currentTags.Where(item => !skillIds.Contains(item.SkillId)));
            database.ExerciseSkills.AddRange(skillIds.Except(currentTags.Select(item => item.SkillId))
                .Select(skillId => new ExerciseSkill { ExerciseId = exercise.Id, SkillId = skillId }));
        }
        exercise.AudioMediaId = input.AudioMediaId;
        exercise.EvaluationRules = TextEvaluationRulesCodec.Write(new TextEvaluationRules(input.IgnoreCase, input.IgnoreTerminalPunctuation));
        if (input.Type == "Matching")
        {
            string[] keys = matchPairs.Select(_ => Guid.NewGuid().ToString("N")).ToArray();
            database.ExerciseOptions.AddRange(matchPairs.Select((pair, index) => new ExerciseOption
            {
                ExerciseId = exercise.Id,
                Text = pair.Left.Trim(),
                AudioMediaId = pair.LeftAudioMediaId,
                OrderNumber = index + 1,
                MatchKey = keys[index]
            }));
            database.ExerciseOptions.AddRange(matchPairs.Select((pair, index) => new ExerciseOption
            {
                ExerciseId = exercise.Id,
                Text = pair.Right.Trim(),
                OrderNumber = matchPairs.Length + index + 1,
                MatchKey = keys[index]
            }));
        }
        var choices = input.Options.Select((text, index) => new ExerciseOption { ExerciseId = exercise.Id, Text = text, OrderNumber = index + 1 }).ToArray();
        database.ExerciseOptions.AddRange(choices);
        if (input.Type == "SentenceBuilder")
        {
            database.AcceptedAnswers.AddRange(tokenOrders.Select(order => new AcceptedAnswer
            {
                ExerciseId = exercise.Id,
                AnswerText = string.Join(" ", order.Select(position => choices[position - 1].Text)),
                OrderedTokenIds = System.Text.Json.JsonSerializer.Serialize(order.Select(position => choices[position - 1].Id).ToArray())
            }));
        }
        else database.AcceptedAnswers.AddRange(input.Answers.Select(text => new AcceptedAnswer { ExerciseId = exercise.Id, AnswerText = text }));
        content.Audit(actorId, lessonId, exerciseId.HasValue ? "DraftExerciseEdited" : "DraftExerciseAdded", "Draft exercise content and answer key updated.", traceId);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(exercise.Id);
    }

    public async Task<ContentOutcome> MoveAsync(Guid actorId, Guid familyId, Guid lessonId, Guid versionId,
        Guid exerciseId, Guid neighborId, bool earlier, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var state = await content.LessonAsync(actorId, familyId, lessonId, cancellationToken);
        if (state is not { } context || !context.Actor.CanEdit(context.Course.TargetLanguageId, context.Lesson.OwnerId))
            return new(Forbidden: true);
        if (context.Version.Id != versionId || context.Version.Status != "Draft")
            return new(Error: "Only the current draft can be reordered.");

        var exercises = await database.Exercises.Where(item => item.LessonVersionId == versionId)
            .OrderBy(item => item.OrderNumber).ToArrayAsync(cancellationToken);
        int index = Array.FindIndex(exercises, item => item.Id == exerciseId);
        if (index < 0) return new(Forbidden: true);
        int target = index + (earlier ? -1 : 1);
        if (target < 0 || target >= exercises.Length || exercises[target].Id != neighborId)
            return new(Error: "The exercise order changed. Refresh the draft and try again.");

        var exercise = exercises[index];
        var neighbor = exercises[target];
        int position = exercise.OrderNumber;
        var occupied = exercises.Select(item => item.OrderNumber).ToHashSet();
        // Use a free positive position while swapping the unique order numbers.
        exercise.OrderNumber = Enumerable.Range(1, exercises.Length + 1).First(number => !occupied.Contains(number));
        await database.SaveChangesAsync(cancellationToken);
        int destination = neighbor.OrderNumber;
        neighbor.OrderNumber = position;
        await database.SaveChangesAsync(cancellationToken);
        exercise.OrderNumber = destination;
        content.Audit(actorId, lessonId, "DraftExerciseMoved", "Draft exercise order changed.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(exerciseId);
    }

    public async Task<ContentOutcome> RemoveAsync(Guid actorId, Guid familyId, Guid lessonId, Guid exerciseId, Guid versionId, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var state = await content.LessonAsync(actorId, familyId, lessonId, cancellationToken);
        if (state is not { } context || !context.Actor.CanEdit(context.Course.TargetLanguageId, context.Lesson.OwnerId)) return new(Forbidden: true);
        if (context.Version.Id != versionId || context.Version.Status != "Draft") return new(Error: "Only current draft exercises can be removed.");
        var exercise = await database.Exercises.SingleOrDefaultAsync(item => item.Id == exerciseId && item.LessonVersionId == versionId, cancellationToken);
        if (exercise is null) return new(Forbidden: true);
        database.ExerciseOptions.RemoveRange(await database.ExerciseOptions.Where(item => item.ExerciseId == exerciseId).ToArrayAsync(cancellationToken));
        database.AcceptedAnswers.RemoveRange(await database.AcceptedAnswers.Where(item => item.ExerciseId == exerciseId).ToArrayAsync(cancellationToken));
        await database.SaveChangesAsync(cancellationToken);
        database.Exercises.Remove(exercise);
        content.Audit(actorId, lessonId, "DraftExerciseRemoved", "Exercise removed from an unpublished draft.", traceId);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(exerciseId);
    }
}
