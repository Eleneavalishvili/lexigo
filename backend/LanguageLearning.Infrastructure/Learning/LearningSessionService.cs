using LanguageLearning.Core.Content;
using System.Security.Cryptography;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Learning.LearningQueries;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class LearningSessionService(LexiGoDbContext database, LearningAccess access, ContentTransactions content,
    LessonValidation validation, LearningQueries queries, TimeProvider clock, HeartBalanceService hearts,
    SystemSettings settings)
{
    public async Task<LearningOutcome> StartAsync(Guid userId, Guid familyId, Guid lessonId, bool practice, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var parent = await (from lesson in database.Lessons
                            join unit in database.Units on lesson.UnitId equals unit.Id
                            where lesson.Id == lessonId
                            select new { unit.CourseId }).SingleOrDefaultAsync(cancellationToken);
        if (parent is null) return new(Missing: true);
        var course = await content.LockCourseAsync(parent.CourseId, cancellationToken);
        var lessonRow = await database.Lessons.SingleAsync(item => item.Id == lessonId, cancellationToken);
        if (course is not { IsPublished: true } || lessonRow.Status != "Published" || lessonRow.CurrentVersionId is not { } versionId) return new(Missing: true);
        if (await database.Units.AnyAsync(unit => unit.Id == lessonRow.UnitId && unit.ArchivedAt != null, cancellationToken))
            return new(Missing: true);
        if (!await database.Enrollments.AnyAsync(item => item.UserId == userId && item.CourseId == course.Id, cancellationToken)) return new(Error: "Join this course before starting a lesson.");
        bool alreadyLearned = await database.LessonProgress.AnyAsync(item => item.UserId == userId && item.LessonId == lessonId && item.Status == "Completed", cancellationToken);
        var bypassed = await PlacementAccess.BypassedAsync(database, userId, course.Id, cancellationToken);
        if (!alreadyLearned && await (from edge in database.LessonPrerequisites
                                      where edge.LessonId == lessonId
                                      where !bypassed.Contains(edge.PrerequisiteLessonId)
                                      where !database.LessonProgress.Any(item => item.UserId == userId && item.LessonId == edge.PrerequisiteLessonId && item.Status == "Completed")
                                      select edge).AnyAsync(cancellationToken)) return new(Error: "Finish the prerequisite lessons first.");
        string mode = practice ? "Practice" : "Lesson";
        var existing = await database.LearningSessions.SingleOrDefaultAsync(item => item.UserId == userId && item.LessonId == lessonId && item.Mode == mode && item.CompletedAt == null, cancellationToken);
        if (existing is not null) return new(await queries.SessionAsync(userId, existing.Id, cancellationToken));
        var balance = practice ? null : await hearts.LockedAsync(userId, cancellationToken);
        if (balance?.Count == 0) return new(Error: "Your hearts are recovering. Wait for the refill or use free practice.");
        if (await database.LearningSessions.CountAsync(item => item.UserId == userId && item.CompletedAt == null, cancellationToken) >= 20) return new(Error: "Finish an open lesson before starting another.");
        string? error = await validation.ValidateAsync(versionId, cancellationToken);
        if (error is not null) return new(Error: "This lesson is not ready to start.");
        var version = await database.LessonVersions.SingleAsync(item => item.Id == versionId, cancellationToken);
        var exercises = await database.Exercises.Where(item => item.LessonVersionId == versionId).OrderBy(item => item.OrderNumber).ToArrayAsync(cancellationToken);
        var ids = exercises.Select(item => item.Id).ToArray();
        var skillTags = (await database.ExerciseSkills.Where(item => ids.Contains(item.ExerciseId))
            .OrderBy(item => item.SkillId).ToArrayAsync(cancellationToken)).ToLookup(item => item.ExerciseId);
        var courseSkills = (await database.Skills.Where(item => item.CourseId == course.Id)
            .OrderBy(item => item.Id).ToArrayAsync(cancellationToken)).ToLookup(item => item.Category);
        var options = await database.ExerciseOptions.Where(item => ids.Contains(item.ExerciseId)).OrderBy(item => item.OrderNumber).ToArrayAsync(cancellationToken);
        var mediaIds = exercises.Where(item => item.AudioMediaId.HasValue).Select(item => item.AudioMediaId!.Value)
            .Concat(options.Where(item => item.AudioMediaId.HasValue).Select(item => item.AudioMediaId!.Value)).Distinct().Order().ToArray();
        var lockedMedia = await ReviewedAudio.LockAsync(database, mediaIds, cancellationToken);
        if (ReviewedAudio.ForLanguage(lockedMedia.AsQueryable(), course.TargetLanguageId).Count() != mediaIds.Length)
            return new(Error: "This lesson's audio is not ready to start.");
        var audio = lockedMedia.ToDictionary(item => item.Id);
        var answers = await database.AcceptedAnswers.Where(item => ids.Contains(item.ExerciseId)).ToArrayAsync(cancellationToken);
        var session = new LearningSession
        {
            UserId = userId,
            CourseId = course.Id,
            LessonId = lessonId,
            LessonVersionId = versionId,
            Title = version.Title,
            Explanation = version.Explanation,
            Mode = mode,
            FirstCompletionXp = practice ? 0 : (await settings.ReadAsync(cancellationToken)).Defaults.LessonXp,
            Hearts = balance?.Count ?? 5,
            StartedAt = clock.GetUtcNow()
        };
        database.LearningSessions.Add(session);
        int order = 0;
        foreach (var exercise in exercises)
        {
            var row = new SessionExercise { UserId = userId, SessionId = session.Id, ExerciseId = exercise.Id, AudioMediaId = exercise.AudioMediaId, OrderNumber = ++order, PublicSnapshot = "{}", PrivateEvaluationSnapshot = "{}" };
            var exerciseOptions = options.Where(item => item.ExerciseId == exercise.Id).ToArray();
            var keys = exercise.Type == "Matching" ? MatchingAnswerKeys.Read(exerciseOptions)! : new Dictionary<Guid, Guid>();
            var choices = exerciseOptions.Select(item => new LearningOption(item.Id, item.Text,
                exercise.Type == "Matching" ? (keys.ContainsKey(item.Id) ? "left" : "right") : null,
                item.AudioMediaId is { } optionMedia ? new AudioInformation(audio[optionMedia].Source, audio[optionMedia].License, audio[optionMedia].DurationMilliseconds!.Value) : null)).ToArray();
            var accepted = answers.Where(item => item.ExerciseId == exercise.Id).Select(item => item.AnswerText).ToArray();
            if (ExerciseFormats.IsChoice(exercise.Type, exerciseOptions.Length) || exercise.Type is "SentenceBuilder" or "Matching") RandomNumberGenerator.Shuffle(choices.AsSpan());
            var tokenOrders = answers.Where(item => item.ExerciseId == exercise.Id && item.OrderedTokenIds != null)
                .Select(item => SentenceAnswerKeys.Read(item.OrderedTokenIds)!).ToArray();
            var clip = exercise.AudioMediaId is { } clipId ? audio[clipId] : null;
            row.PublicSnapshot = Encode(new ExerciseView(row.Id, exercise.Type, exercise.Prompt, choices, exercise.ContextText,
                clip is null ? null : new AudioInformation(clip.Source, clip.License, clip.DurationMilliseconds!.Value)));
            var rules = TextEvaluationRulesCodec.Read(exercise.EvaluationRules)!;
            int[] skillIds = skillTags[exercise.Id].Select(item => item.SkillId).ToArray();
            if (skillIds.Length == 0 && ExerciseSkillTracking.DefaultCategory(exercise.Type) is { } category)
                skillIds = courseSkills[category].Select(item => item.Id).ToArray();
            row.PrivateEvaluationSnapshot = Encode(new EvaluationSnapshot(accepted,
                choices.Where(item => accepted.Contains(item.Text)).Select(item => item.Id).ToArray(),
                exercise.Explanation, rules.IgnoreCase, rules.IgnoreTerminalPunctuation, tokenOrders,
                keys.Select(pair => new MatchedPair(pair.Key, pair.Value)).ToArray(), skillIds));
            database.SessionExercises.Add(row);
            database.SessionOptionAudio.AddRange(exerciseOptions.Where(item => item.AudioMediaId.HasValue).Select(item => new SessionOptionAudio
            {
                SessionExerciseId = row.Id,
                OptionId = item.Id,
                AudioMediaId = item.AudioMediaId!.Value
            }));
        }
        if (!practice && !await database.LessonProgress.AnyAsync(item => item.UserId == userId && item.LessonId == lessonId, cancellationToken))
            database.LessonProgress.Add(new LessonProgress { UserId = userId, LessonId = lessonId });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(await queries.SessionAsync(userId, session.Id, cancellationToken), Created: true);
    }
}
