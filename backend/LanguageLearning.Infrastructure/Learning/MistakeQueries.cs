using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Learning.LearningQueries;

namespace LanguageLearning.Infrastructure.Learning;

public sealed record MistakeItem(Guid ExerciseId, string Prompt, string Type, string? ContextText,
    int WrongCount, DateTimeOffset? LastMissedAt, DateTimeOffset? DueAt, DateTimeOffset LastAttemptAt,
    string[] Skills);
public sealed record MistakePage(MistakeItem[] Items, long TotalCount, int Page, bool AdvancedPracticeAvailable = false)
{
    internal const int MaximumPageSize = 20;
    public int PageSize => MaximumPageSize;
}

internal sealed class MistakeCandidate
{
    public Guid ExerciseId { get; set; }
    public Guid SourceSessionExerciseId { get; set; }
    public Guid LessonVersionId { get; set; }
    public Guid? AudioMediaId { get; set; }
    public required string Explanation { get; set; }
    public required string PublicSnapshot { get; set; }
    public required string PrivateEvaluationSnapshot { get; set; }
    public int WrongCount { get; set; }
    public DateTimeOffset? LastMissedAt { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public bool IsWeakSkill { get; set; }
    public long TotalCount { get; set; }
}

public sealed class MistakeQueries(LexiGoDbContext database, TimeProvider clock, SubscriptionQueries subscriptions)
{
    internal const string AdvancedPractice = "AdvancedPractice";
    private static readonly string[] ReviewTypes =
        ["MultipleChoice", "Translation", "FillBlank", "SentenceBuilder", "Matching", "Reading", "DialogueChoice", "Alphabet", "Listening"];

    public async Task<MistakePage?> ListAsync(Guid userId, Guid courseId, int page, CancellationToken cancellationToken,
        int? skillId = null, bool includeDue = false)
    {
        if (!await database.Enrollments.AnyAsync(item => item.UserId == userId && item.CourseId == courseId, cancellationToken)) return null;
        var skill = skillId.HasValue ? await FindSkillAsync(courseId, skillId.Value, cancellationToken) : null;
        if (skillId.HasValue && skill is null) return null;
        var rows = await CandidatesAsync(userId, courseId, (page - 1) * MistakePage.MaximumPageSize,
            MistakePage.MaximumPageSize, cancellationToken, skill, includeDue);
        long total = rows.FirstOrDefault()?.TotalCount ?? (page > 1
            ? (await CandidatesAsync(userId, courseId, 0, 1, cancellationToken, skill, includeDue)).FirstOrDefault()?.TotalCount ?? 0 : 0);
        var skills = rows.Count == 0 ? [] : await database.Skills.AsNoTracking()
            .Where(item => item.CourseId == courseId).OrderBy(item => item.Name).ToArrayAsync(cancellationToken);
        return new(rows.Select(row =>
        {
            var question = Decode<ExerciseView>(row.PublicSnapshot);
            var savedSkills = Decode<EvaluationSnapshot>(row.PrivateEvaluationSnapshot).SkillIds;
            string? category = ExerciseSkillTracking.DefaultCategory(question.Type);
            var names = skills.Where(item => savedSkills is null ? item.Category == category : savedSkills.Contains(item.Id))
                .Select(item => item.Name).Distinct().ToArray();
            return new MistakeItem(row.ExerciseId, question.Prompt, question.Type, question.ContextText,
                row.WrongCount, row.LastMissedAt, row.DueAt, row.LastAttemptAt, names);
        }).ToArray(), total, page, await subscriptions.HasAsync(userId, AdvancedPractice, cancellationToken));
    }

    internal Task<Skill?> FindSkillAsync(Guid courseId, int skillId, CancellationToken cancellationToken) =>
        database.Skills.AsNoTracking().SingleOrDefaultAsync(item => item.Id == skillId && item.CourseId == courseId, cancellationToken);

    // Rank full owned history before paging, so old unresolved errors are not silently dropped.
    // Equal-time conflicting outcomes conservatively retain the incorrect one.
    internal Task<List<MistakeCandidate>> CandidatesAsync(Guid userId, Guid courseId, int offset, int take,
        CancellationToken cancellationToken, Skill? skill = null, bool includeDue = false, string? focus = null)
    {
        int skillId = skill?.Id ?? 0;
        var legacyTypes = ReviewTypes.Where(type => ExerciseSkillTracking.DefaultCategory(type) == skill?.Category).ToArray();
        var categories = Encode(ReviewTypes.ToDictionary(type => type, ExerciseSkillTracking.DefaultCategory));
        var now = clock.GetUtcNow();
        bool dueOnly = focus == "due";
        bool repeatedOnly = focus == "repeated";
        return database.Database.SqlQuery<MistakeCandidate>($"""
            WITH history AS (
                SELECT e."ExerciseId", e."Id" AS "SourceSessionExerciseId", s."LessonVersionId",
                       s."Explanation", e."PublicSnapshot", e."PrivateEvaluationSnapshot",
                       e."AudioMediaId", a."IsCorrect", a."SubmittedAt", a."Id" AS "AttemptId", s."Mode",
                       (COUNT(*) FILTER (WHERE NOT a."IsCorrect") OVER exercise_history)::int AS "WrongCount",
                       MAX(a."SubmittedAt") FILTER (WHERE NOT a."IsCorrect") OVER exercise_history AS "LastMissedAt"
                FROM "AnswerAttempts" a
                JOIN "SessionExercises" e ON e."Id" = a."SessionExerciseId" AND e."UserId" = a."UserId"
                JOIN "LearningSessions" s ON s."Id" = e."SessionId" AND s."UserId" = a."UserId"
                WHERE a."UserId" = {userId} AND s."CourseId" = {courseId}
                  AND a."IsGraded" AND s."CompletedAt" IS NOT NULL AND s."PlacementMap" IS NULL
                WINDOW exercise_history AS (PARTITION BY e."ExerciseId")
            ), recent_results AS (
                SELECT AVG(result."IsCorrect"::int) AS accuracy
                FROM (
                    SELECT "IsCorrect" FROM history
                    ORDER BY "SubmittedAt" DESC, "AttemptId" DESC LIMIT 10
                ) result
                HAVING COUNT(*) >= 5
            ), repetitions AS (
                SELECT history.*, COUNT(*) FILTER (
                    WHERE "IsCorrect" AND "Mode" = 'Practice'
                      AND ("LastMissedAt" IS NULL OR "SubmittedAt" > "LastMissedAt")
                ) OVER (PARTITION BY "ExerciseId") AS successes
                FROM history
            ), latest AS (
                SELECT DISTINCT ON ("ExerciseId") repetitions.*,
                    CASE WHEN "IsCorrect" THEN "SubmittedAt"
                        + (ARRAY[24, 72, 168, 336, 720])[LEAST(successes, 4)::int + 1] * INTERVAL '1 hour'
                    END AS "DueAt"
                FROM repetitions
                ORDER BY "ExerciseId", "SubmittedAt" DESC, "IsCorrect", "AttemptId" DESC
            ), ready_audio AS NOT MATERIALIZED (
                SELECT media."Id" FROM "MediaAssets" media
                JOIN "Courses" course ON course."TargetLanguageId" = media."LanguageId"
                WHERE course."Id" = {courseId} AND media."StorageState" = 'Ready' AND media."IsVerified"
                  AND media."VerifiedBy" IS NOT NULL AND media."VerifiedAt" IS NOT NULL
                  AND media."Sha256" IS NOT NULL AND media."DurationMilliseconds" IS NOT NULL
                  AND media."Source" <> '' AND media."License" <> ''
            ), eligible AS (
                SELECT * FROM latest l WHERE (NOT "IsCorrect" OR ({includeDue} AND "DueAt" <= {now}))
                  AND (NOT {dueOnly} OR "IsCorrect")
                  AND (NOT {repeatedOnly} OR (NOT "IsCorrect" AND "WrongCount" >= 2))
                  AND "PublicSnapshot"->>'type' = ANY({ReviewTypes})
                  AND ("AudioMediaId" IS NOT NULL OR "PublicSnapshot"->>'type' <> 'Listening')
                  AND ("AudioMediaId" IS NULL OR "AudioMediaId" IN (SELECT "Id" FROM ready_audio))
                  AND ({skillId} = 0 OR CASE
                    WHEN jsonb_typeof("PrivateEvaluationSnapshot"->'skillIds') = 'array'
                        THEN ("PrivateEvaluationSnapshot"->'skillIds') @> jsonb_build_array({skillId})
                    ELSE "PublicSnapshot"->>'type' = ANY({legacyTypes})
                  END)
                  AND NOT EXISTS (
                    SELECT 1 FROM "SessionOptionAudio" o WHERE o."SessionExerciseId" = l."SourceSessionExerciseId"
                      AND o."AudioMediaId" NOT IN (SELECT "Id" FROM ready_audio)
                  )
            ), ranked AS (
                SELECT eligible.*, weak.failure_rate,
                    CASE
                        WHEN recent.accuracy < 0.60 THEN level.step
                        WHEN recent.accuracy >= 0.80 THEN -level.step
                    END AS difficulty_priority
                FROM eligible
                JOIN "Exercises" exercise ON exercise."Id" = eligible."ExerciseId"
                JOIN "LessonVersions" version ON version."Id" = exercise."LessonVersionId"
                JOIN "Lessons" lesson ON lesson."Id" = version."LessonId"
                JOIN "Units" unit ON unit."Id" = lesson."UnitId"
                LEFT JOIN recent_results recent ON TRUE
                CROSS JOIN LATERAL (
                    SELECT CASE unit."Difficulty"
                        WHEN 'A1' THEN 1 WHEN 'Beginner' THEN 1
                        WHEN 'A2' THEN 2
                        WHEN 'B1' THEN 3 WHEN 'Intermediate' THEN 3
                        WHEN 'B2' THEN 4
                        WHEN 'C1' THEN 5 WHEN 'Advanced' THEN 5
                        WHEN 'C2' THEN 6
                    END AS step
                ) level
                LEFT JOIN LATERAL (
                    SELECT MAX(progress."IncorrectCount"::numeric /
                        (progress."CorrectCount"::numeric + progress."IncorrectCount")) AS failure_rate
                    FROM "SkillProgress" progress
                    JOIN "Skills" skill ON skill."Id" = progress."SkillId"
                    WHERE progress."UserId" = {userId} AND skill."CourseId" = {courseId}
                      AND ({skillId} = 0 OR skill."Id" = {skillId})
                      AND progress."IncorrectCount" > progress."CorrectCount"
                      AND CASE WHEN jsonb_typeof(eligible."PrivateEvaluationSnapshot"->'skillIds') = 'array'
                        THEN (eligible."PrivateEvaluationSnapshot"->'skillIds') @> jsonb_build_array(skill."Id")
                        ELSE skill."Category" = ({categories}::jsonb ->> (eligible."PublicSnapshot"->>'type'))
                      END
                ) weak ON NOT eligible."IsCorrect"
            )
            SELECT "ExerciseId", "SourceSessionExerciseId", "LessonVersionId", "Explanation", "AudioMediaId",
                   "PublicSnapshot"::text AS "PublicSnapshot", "PrivateEvaluationSnapshot"::text AS "PrivateEvaluationSnapshot",
                   "WrongCount", "LastMissedAt", "SubmittedAt" AS "LastAttemptAt", "DueAt", failure_rate IS NOT NULL AS "IsWeakSkill",
                   COUNT(*) OVER () AS "TotalCount"
            FROM ranked ORDER BY "IsCorrect", failure_rate DESC NULLS LAST, "WrongCount" DESC,
                difficulty_priority NULLS LAST, COALESCE("DueAt", "SubmittedAt"), "ExerciseId"
            OFFSET {offset} LIMIT {take}
            """).ToListAsync(cancellationToken);
    }
}
