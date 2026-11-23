using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillSkillProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                LOCK TABLE "AnswerAttempts", "SkillProgress" IN SHARE ROW EXCLUSIVE MODE;

                WITH observations AS (
                    SELECT attempt."UserId", session."CourseId", attempt."IsCorrect", attempt."SubmittedAt",
                           exercise."PrivateEvaluationSnapshot"->'skillIds' AS skill_ids,
                           CASE exercise."PublicSnapshot"->>'type'
                               WHEN 'MultipleChoice' THEN 'vocabulary'
                               WHEN 'Translation' THEN 'vocabulary'
                               WHEN 'Matching' THEN 'vocabulary'
                               WHEN 'FillBlank' THEN 'grammar'
                               WHEN 'SentenceBuilder' THEN 'grammar'
                               WHEN 'Reading' THEN 'reading'
                               WHEN 'DialogueChoice' THEN 'reading'
                               WHEN 'Listening' THEN 'listening'
                               WHEN 'Alphabet' THEN 'alphabet'
                           END AS legacy_category
                    FROM "AnswerAttempts" attempt
                    JOIN "SessionExercises" exercise ON exercise."Id" = attempt."SessionExerciseId"
                        AND exercise."UserId" = attempt."UserId"
                    JOIN "LearningSessions" session ON session."Id" = exercise."SessionId"
                        AND session."UserId" = attempt."UserId"
                    JOIN "Users" learner ON learner."Id" = attempt."UserId"
                    WHERE attempt."IsGraded" AND session."PlacementMap" IS NULL AND learner."Status" <> 'Deleted'
                )
                INSERT INTO "SkillProgress" ("UserId", "SkillId", "CorrectCount", "IncorrectCount", "LastPracticedAt")
                SELECT observation."UserId", skill."Id",
                       COUNT(*) FILTER (WHERE observation."IsCorrect"),
                       COUNT(*) FILTER (WHERE NOT observation."IsCorrect"),
                       MAX(observation."SubmittedAt")
                FROM observations observation
                JOIN "Skills" skill ON skill."CourseId" = observation."CourseId"
                    AND CASE WHEN jsonb_typeof(observation.skill_ids) = 'array'
                        THEN observation.skill_ids @> jsonb_build_array(skill."Id")
                        ELSE skill."Category" = observation.legacy_category
                    END
                GROUP BY observation."UserId", skill."Id"
                ON CONFLICT ("UserId", "SkillId") DO UPDATE
                SET "CorrectCount" = EXCLUDED."CorrectCount",
                    "IncorrectCount" = EXCLUDED."IncorrectCount",
                    "LastPracticedAt" = EXCLUDED."LastPracticedAt";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // These rows contain derived totals alongside live progress. Preserve both on rollback.
        }
    }
}
