using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

internal static class LeaderboardScores
{
    internal static IQueryable<LeaderboardRow> For(LexiGoDbContext database, Guid courseId, DateOnly week)
    {
        DateTimeOffset start = new(week.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        DateTimeOffset end = start.AddDays(7);
        return database.Database.SqlQuery<LeaderboardRow>($"""
            WITH scores AS (
                SELECT xp."UserId", users."DisplayName", sum(xp."Amount")::bigint AS "Xp",
                       max(xp."EarnedAt") AS achieved_at
                FROM "XpTransactions" xp
                JOIN "Users" users ON users."Id" = xp."UserId"
                JOIN "UserPreferences" prefs ON prefs."UserId" = users."Id"
                WHERE xp."CourseId" = {courseId} AND xp."EarnedAt" >= {start} AND xp."EarnedAt" < {end}
                  AND prefs."LeaderboardOptIn" AND users."Status" = 'Active' AND users."EmailConfirmed"
                GROUP BY xp."UserId", users."DisplayName"
            )
            SELECT "UserId", "DisplayName", "Xp",
                   row_number() OVER (ORDER BY "Xp" DESC, achieved_at, "UserId")::integer AS "Rank"
            FROM scores
            """);
    }
}
