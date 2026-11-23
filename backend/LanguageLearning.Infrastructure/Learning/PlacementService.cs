using System.Security.Cryptography;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Learning.LearningQueries;

namespace LanguageLearning.Infrastructure.Learning;

internal sealed record PlacementUnitSnapshot(Guid Id, string Title, string Level, Guid[] LessonIds, Guid[] QuestionIds);
public sealed record PlacementUnitScore(Guid Id, string Title, int Correct, int Total);
public sealed record PlacementResult(Guid SessionId, Guid CourseId, bool Completed, bool Applied,
    Guid? RecommendedUnitId, string? RecommendedTitle, PlacementUnitScore[] Units);

public sealed class PlacementService(LexiGoDbContext database, LearningAccess access,
    ContentTransactions content, LearningQueries queries, TimeProvider clock)
{
    public async Task<LearningOutcome> StartAsync(Guid userId, Guid familyId, Guid courseId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var course = await content.LockCourseAsync(courseId, cancellationToken);
        if (course is not { IsPublished: true }) return new(Missing: true);
        if (!await database.Enrollments.AnyAsync(item => item.UserId == userId && item.CourseId == courseId, cancellationToken))
            return new(Error: "Join this course before checking your starting point.");
        var open = await database.LearningSessions.SingleOrDefaultAsync(item => item.UserId == userId
            && item.CourseId == courseId && item.PlacementMap != null && item.CompletedAt == null, cancellationToken);
        if (open is not null) return new(await queries.SessionAsync(userId, open.Id, cancellationToken));
        if (await database.LearningSessions.AnyAsync(item => item.UserId == userId && item.CourseId == courseId
            && item.PlacementMap != null && item.StartedAt > clock.GetUtcNow().AddDays(-1), cancellationToken))
            return new(Error: "A new placement check is available 24 hours after your previous one.");
        if (await database.LearningSessions.CountAsync(item => item.UserId == userId && item.CompletedAt == null, cancellationToken) >= 20)
            return new(Error: "Finish an open learning step before starting another.");

        var units = await database.Units.Where(item => item.CourseId == courseId && item.ArchivedAt == null)
            .OrderBy(item => item.OrderNumber).ToArrayAsync(cancellationToken);
        var lessons = await (from lesson in database.Lessons
                             join version in database.LessonVersions on lesson.CurrentVersionId equals version.Id
                             where units.Select(item => item.Id).Contains(lesson.UnitId) && lesson.Status == "Published"
                             && !version.Title.StartsWith("Unit check")
                             select new { lesson.Id, lesson.UnitId, VersionId = version.Id }).ToArrayAsync(cancellationToken);
        units = units.Where(unit => lessons.Any(lesson => lesson.UnitId == unit.Id)).ToArray();
        if (units.Length is < 1 or > 12) return new(Error: "A placement bank must be configured for this course before it can be used.");
        var versionIds = lessons.Select(item => item.VersionId).ToArray();
        var bank = await database.Exercises.Where(item => versionIds.Contains(item.LessonVersionId)
            && (item.Type == "Translation" || item.Type == "MultipleChoice" || item.Type == "Reading")
            && item.AudioMediaId == null).ToArrayAsync(cancellationToken);
        var bankVersions = bank.Select(item => item.LessonVersionId).ToHashSet();
        var placementUnits = lessons.Where(item => bankVersions.Contains(item.VersionId))
            .Select(item => item.UnitId).ToHashSet();
        units = units.Where(item => placementUnits.Contains(item.Id)).ToArray();
        if (units.Length == 0) return new(Error: "A placement bank must be configured for this course before it can be used.");
        var ids = bank.Select(item => item.Id).ToArray();
        var options = await database.ExerciseOptions.Where(item => ids.Contains(item.ExerciseId)).ToArrayAsync(cancellationToken);
        var answers = await database.AcceptedAnswers.Where(item => ids.Contains(item.ExerciseId)).ToArrayAsync(cancellationToken);
        var session = new LearningSession
        {
            UserId = userId,
            CourseId = courseId,
            LessonVersionId = lessons.First(item => bankVersions.Contains(item.VersionId)).VersionId,
            Mode = "Practice",
            Title = "Find your starting point",
            Explanation = "Answer from memory. This course-specific check does not use hearts or award XP. Your result recommends a published unit; it is not a CEFR certificate.",
            FirstCompletionXp = 0,
            StartedAt = clock.GetUtcNow()
        };
        var mapping = new List<PlacementUnitSnapshot>();
        int order = 0;
        foreach (var unit in units)
        {
            var unitLessons = lessons.Where(item => item.UnitId == unit.Id).ToArray();
            var unitVersions = unitLessons.Select(item => item.VersionId).ToArray();
            var candidates = bank.Where(item => unitVersions.Contains(item.LessonVersionId)).ToArray();
            RandomNumberGenerator.Shuffle(candidates.AsSpan());
            // Keep several lesson topics represented rather than sampling only one phrase.
            var selected = candidates.GroupBy(item => item.LessonVersionId).Select(group => group.First())
                .Concat(candidates).DistinctBy(item => item.Id).Take(5).ToArray();
            if (selected.Length != 5) return new(Error: "Each placement unit needs at least five published text questions.");
            var questionIds = new List<Guid>();
            foreach (var exercise in selected)
            {
                var accepted = answers.Where(item => item.ExerciseId == exercise.Id).Select(item => item.AnswerText).ToArray();
                var choices = options.Where(item => item.ExerciseId == exercise.Id && item.AudioMediaId == null)
                    .Select(item => new LearningOption(item.Id, item.Text)).ToArray();
                var rules = TextEvaluationRulesCodec.Read(exercise.EvaluationRules);
                if (rules is null || accepted.Length == 0 || (exercise.Type != "Translation"
                    && (choices.Length < 2 || !accepted.All(answer => choices.Any(choice => choice.Text == answer)))))
                    return new(Error: "This placement bank needs a content review before use.");
                RandomNumberGenerator.Shuffle(choices.AsSpan());
                var row = new SessionExercise
                {
                    UserId = userId,
                    SessionId = session.Id,
                    ExerciseId = exercise.Id,
                    OrderNumber = ++order,
                    PublicSnapshot = Encode(new ExerciseView(Guid.Empty, exercise.Type, exercise.Prompt, choices, exercise.ContextText)),
                    PrivateEvaluationSnapshot = Encode(new EvaluationSnapshot(accepted,
                        choices.Where(choice => accepted.Contains(choice.Text)).Select(choice => choice.Id).ToArray(),
                        exercise.Explanation, rules.IgnoreCase, rules.IgnoreTerminalPunctuation))
                };
                row.PublicSnapshot = Encode(Decode<ExerciseView>(row.PublicSnapshot) with { Id = row.Id });
                database.SessionExercises.Add(row);
                questionIds.Add(row.Id);
            }
            mapping.Add(new(unit.Id, unit.Title, unit.Difficulty, unitLessons.Select(item => item.Id).ToArray(), questionIds.ToArray()));
        }
        session.PlacementMap = Encode(mapping);
        database.LearningSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(await queries.SessionAsync(userId, session.Id, cancellationToken), Created: true);
    }

    public async Task<PlacementResult?> ResultAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await database.LearningSessions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sessionId
            && item.UserId == userId && item.PlacementMap != null, cancellationToken);
        return session is null ? null : await DescribeAsync(session, cancellationToken);
    }

    public async Task<LearningOutcome> ApplyAsync(Guid userId, Guid familyId, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var session = await database.LearningSessions.SingleOrDefaultAsync(item => item.Id == sessionId
            && item.UserId == userId && item.PlacementMap != null, cancellationToken);
        if (session is null) return new(Missing: true);
        var result = await DescribeAsync(session, cancellationToken);
        if (!result.Completed) return new(Error: "Finish every placement question and complete the check first.");
        if (session.PlacementAppliedAt is not null) return new(result);
        var target = await database.Units.SingleOrDefaultAsync(item => item.Id == result.RecommendedUnitId
            && item.CourseId == session.CourseId, cancellationToken);
        if (target is null || target.ArchivedAt.HasValue || !await database.Courses.AnyAsync(item => item.Id == session.CourseId && item.IsPublished, cancellationToken)
            || !await database.Lessons.AnyAsync(item => item.UnitId == target.Id && item.Status == "Published", cancellationToken))
            return new(Error: "The recommended unit is no longer available. Your saved progress is unchanged.");
        session.RecommendedUnitId = target.Id;
        session.PlacementAppliedAt = clock.GetUtcNow();
        var enrollment = await database.Enrollments.SingleAsync(item => item.UserId == userId && item.CourseId == session.CourseId, cancellationToken);
        var snapshot = Decode<PlacementUnitSnapshot[]>(session.PlacementMap!).Single(item => item.Id == target.Id);
        enrollment.PlacementLevel = "Test:" + (snapshot.Level is "A1" or "A2" or "B1" or "B2" or "C1" or "C2" ? snapshot.Level : "A1");
        database.AuditLogs.Add(new LanguageLearning.Core.Administration.AuditLog
        {
            ActorId = userId,
            Action = "Learning.PlacementApplied",
            EntityType = "LearningSession",
            EntityId = session.Id.ToString(),
            TraceId = "placement/" + session.Id,
            Reason = "Learner applied a server-scored course starting recommendation; no skipped XP awarded.",
            OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(result with { Applied = true });
    }

    private async Task<PlacementResult> DescribeAsync(LearningSession session, CancellationToken cancellationToken)
    {
        var mapping = Decode<PlacementUnitSnapshot[]>(session.PlacementMap!);
        var attempts = await (from attempt in database.AnswerAttempts.AsNoTracking()
                              join exercise in database.SessionExercises on attempt.SessionExerciseId equals exercise.Id
                              where exercise.SessionId == session.Id && attempt.UserId == session.UserId
                              select new { attempt.SessionExerciseId, attempt.IsCorrect }).ToArrayAsync(cancellationToken);
        var scores = mapping.Select(unit => new PlacementUnitScore(unit.Id, unit.Title,
            attempts.Count(item => unit.QuestionIds.Contains(item.SessionExerciseId) && item.IsCorrect), unit.QuestionIds.Length)).ToArray();
        bool complete = session.CompletedAt != null && attempts.Length == mapping.Sum(item => item.QuestionIds.Length);
        var recommended = complete ? scores.FirstOrDefault(unit => unit.Correct * 100 / unit.Total < 80) ?? scores[^1] : null;
        return new(session.Id, session.CourseId, complete, session.PlacementAppliedAt != null,
            recommended?.Id, recommended?.Title, complete ? scores : []);
    }
}
