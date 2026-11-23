using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

internal sealed record WeeklyBounds(DateTimeOffset Start, DateTimeOffset End);
internal sealed record WeeklyStudyDay(DateOnly StudyDate, long EarnedXp);

internal static class WeeklyStudyDays
{
    public static Task<WeeklyBounds> BoundsAsync(LexiGoDbContext database, DateOnly start, string zone, CancellationToken cancellationToken) =>
        database.Database.SqlQuery<WeeklyBounds>($"""
            SELECT ({start}::date::timestamp AT TIME ZONE {zone}) AS "Start",
                   ({start.AddDays(7)}::date::timestamp AT TIME ZONE {zone}) AS "End"
            """).SingleAsync(cancellationToken);

    public static Task<WeeklyStudyDay[]> ReadAsync(LexiGoDbContext database, Guid userId, WeeklyBounds bounds,
        DateTimeOffset now, string zone, CancellationToken cancellationToken) =>
        database.Database.SqlQuery<WeeklyStudyDay>($"""
            SELECT (events.at AT TIME ZONE {zone})::date AS "StudyDate", SUM(events.xp)::bigint AS "EarnedXp"
            FROM (
                SELECT "EarnedAt" AS at, "Amount"::bigint AS xp FROM "XpTransactions"
                WHERE "UserId" = {userId} AND "EarnedAt" >= {bounds.Start} AND "EarnedAt" < {bounds.End} AND "EarnedAt" <= {now}
                UNION ALL
                SELECT answer."SubmittedAt", 0::bigint
                FROM "AnswerAttempts" answer
                JOIN "SessionExercises" exercise ON exercise."Id" = answer."SessionExerciseId" AND exercise."UserId" = {userId}
                JOIN "LearningSessions" session ON session."Id" = exercise."SessionId" AND session."UserId" = {userId}
                WHERE answer."UserId" = {userId} AND session."PlacementMap" IS NULL
                    AND answer."SubmittedAt" >= {bounds.Start} AND answer."SubmittedAt" < {bounds.End} AND answer."SubmittedAt" <= {now}
                UNION ALL
                SELECT review."ReviewedAt", 0::bigint
                FROM "WordReviews" review JOIN "SavedWords" saved ON saved."Id" = review."SavedWordId"
                WHERE saved."UserId" = {userId} AND review."ReviewedAt" >= {bounds.Start}
                    AND review."ReviewedAt" < {bounds.End} AND review."ReviewedAt" <= {now}
                UNION ALL
                SELECT "CompletedAt", 0::bigint FROM "LessonProgress"
                WHERE "UserId" = {userId} AND "Status" = 'Completed' AND "CompletedAt" >= {bounds.Start}
                    AND "CompletedAt" < {bounds.End} AND "CompletedAt" <= {now}
                UNION ALL
                SELECT "CompletedAt", 0::bigint FROM "DialogueSessions"
                WHERE "UserId" = {userId} AND "CompletedAt" >= {bounds.Start}
                    AND "CompletedAt" < {bounds.End} AND "CompletedAt" <= {now}
            ) events
            GROUP BY "StudyDate"
            ORDER BY "StudyDate"
            """).ToArrayAsync(cancellationToken);
}
