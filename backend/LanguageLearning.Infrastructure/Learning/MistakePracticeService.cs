using System.Security.Cryptography;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Learning.LearningQueries;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class MistakePracticeService(LexiGoDbContext database, LearningAccess access, ContentTransactions content,
    MistakeQueries mistakes, LearningQueries queries, SubscriptionQueries subscriptions, TimeProvider clock)
{
    public async Task<bool> IsLockedAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        await database.LearningSessions.AnyAsync(item => item.Id == sessionId && item.UserId == userId
            && item.PracticeFocus != null && item.CompletedAt == null, cancellationToken)
        && !await subscriptions.HasAsync(userId, MistakeQueries.AdvancedPractice, cancellationToken);

    public async Task<LearningOutcome> StartAsync(Guid userId, Guid familyId, Guid courseId,
        CancellationToken cancellationToken, int? skillId = null, string? focus = null)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var course = await content.LockCourseAsync(courseId, cancellationToken);
        if (course is not { IsPublished: true } || !await database.Enrollments.AnyAsync(item => item.UserId == userId
            && item.CourseId == courseId, cancellationToken)) return new(Missing: true);
        var skill = skillId.HasValue ? await mistakes.FindSkillAsync(courseId, skillId.Value, cancellationToken) : null;
        if (skillId.HasValue && skill is null) return new(Missing: true);
        if (focus is not null && !await subscriptions.HasAsync(userId, MistakeQueries.AdvancedPractice, cancellationToken))
            return new(PremiumRequired: true);
        var open = await database.LearningSessions.SingleOrDefaultAsync(item => item.UserId == userId && item.CourseId == courseId
            && item.PracticeSkillId == skillId && item.PracticeFocus == focus
            && item.Mode == "Practice" && item.LessonId == null && item.PlacementMap == null && item.CompletedAt == null, cancellationToken);
        if (open is not null) return new(await queries.SessionAsync(userId, open.Id, cancellationToken));
        if (await database.LearningSessions.CountAsync(item => item.UserId == userId && item.CompletedAt == null, cancellationToken) >= 20)
            return new(Error: "Finish an open learning step before starting another.");
        var candidates = await mistakes.CandidatesAsync(userId, courseId, 0, 50, cancellationToken, skill,
            includeDue: true, focus: focus);
        if (candidates.Count == 0) return new(Error: focus switch
        {
            "due" => "No questions are due for review yet.",
            "repeated" => "No unresolved questions have been missed twice yet.",
            _ => "No saved mistakes are ready to practise. Finish a learning step first; placement checks are not included."
        });
        var selected = new List<MistakeCandidate>();
        var notes = new List<string>();
        int notesLength = 0;
        string? previousType = null;
        int streak = 0;
        var remaining = candidates.Select(candidate =>
            (Candidate: candidate, Question: Decode<ExerciseView>(candidate.PublicSnapshot))).ToList();
        while (selected.Count < 10 && remaining.Count > 0)
        {
            bool due = remaining[0].Candidate.DueAt.HasValue;
            bool weak = remaining[0].Candidate.IsWeakSkill;
            int index = streak >= 2
                ? remaining.FindIndex(item => item.Candidate.DueAt.HasValue == due
                    && item.Candidate.IsWeakSkill == weak && item.Question.Type != previousType)
                : 0;
            if (index < 0) index = 0;
            var (candidate, question) = remaining[index];
            remaining.RemoveAt(index);
            if (!notes.Contains(candidate.Explanation))
            {
                int length = candidate.Explanation.Length + (notes.Count > 0 ? 2 : 0);
                if (notesLength + length > 20000) continue;
                notes.Add(candidate.Explanation);
                notesLength += length;
            }
            selected.Add(candidate);
            streak = question.Type == previousType ? streak + 1 : 1;
            previousType = question.Type;
        }
        if (selected.Count == 0) return new(Error: "These saved questions need a content review before practice.");
        var sourceIds = selected.Select(item => item.SourceSessionExerciseId).ToArray();
        var optionAudio = await database.SessionOptionAudio.AsNoTracking()
            .Where(item => sourceIds.Contains(item.SessionExerciseId)).ToArrayAsync(cancellationToken);
        var mediaIds = selected.Where(item => item.AudioMediaId.HasValue).Select(item => item.AudioMediaId!.Value)
            .Concat(optionAudio.Select(item => item.AudioMediaId)).Distinct().ToArray();
        var media = await ReviewedAudio.LockAsync(database, mediaIds, cancellationToken);
        if (ReviewedAudio.ForLanguage(media.AsQueryable(), course.TargetLanguageId).Count() != mediaIds.Length)
            return new(Error: "Some review audio changed. Try starting practice again.");
        var audioByExercise = optionAudio.ToLookup(item => item.SessionExerciseId);
        var session = new LearningSession
        {
            UserId = userId,
            CourseId = courseId,
            PracticeSkillId = skillId,
            PracticeFocus = focus,
            LessonVersionId = selected[0].LessonVersionId,
            Title = focus switch
            {
                "due" => "Due for review",
                "repeated" => "Repeated mistakes",
                _ => skill is null ? "Your mistake practice" : $"{skill.Name} mistake practice"
            },
            Explanation = string.Join("\n\n", notes),
            Mode = "Practice",
            FirstCompletionXp = 0,
            StartedAt = clock.GetUtcNow()
        };
        database.LearningSessions.Add(session);
        int order = 0;
        foreach (var candidate in selected)
        {
            var question = Decode<ExerciseView>(candidate.PublicSnapshot);
            var choices = question.Options.ToArray();
            RandomNumberGenerator.Shuffle(choices.AsSpan());
            var row = new SessionExercise
            {
                UserId = userId,
                SessionId = session.Id,
                ExerciseId = candidate.ExerciseId,
                AudioMediaId = candidate.AudioMediaId,
                OrderNumber = ++order,
                PrivateEvaluationSnapshot = candidate.PrivateEvaluationSnapshot,
                PublicSnapshot = "{}"
            };
            row.PublicSnapshot = Encode(question with { Id = row.Id, Options = choices });
            database.SessionExercises.Add(row);
            database.SessionOptionAudio.AddRange(audioByExercise[candidate.SourceSessionExerciseId].Select(item => new SessionOptionAudio
            {
                SessionExerciseId = row.Id,
                OptionId = item.OptionId,
                AudioMediaId = item.AudioMediaId
            }));
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(await queries.SessionAsync(userId, session.Id, cancellationToken), Created: true);
    }
}
