using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed class AssignmentDeadlineNotices(LexiGoDbContext database, TimeProvider clock)
{
    public Task<int> SendBatchAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var tomorrow = now.AddHours(24);
        return database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Notifications" ("Id", "UserId", "AssignmentId", "Type", "Title", "Body", "CreatedAt")
            SELECT gen_random_uuid(), member."UserId", assignment."Id", 'AssignmentDue',
                   'Assignment due within 24 hours', assignment."Title", {now}
            FROM "Assignments" assignment
            JOIN "Classrooms" classroom ON classroom."Id" = assignment."ClassroomId"
            JOIN "ClassMemberships" member ON member."ClassroomId" = classroom."Id"
            JOIN "Users" learner ON learner."Id" = member."UserId"
            WHERE assignment."DueAt" > {now} AND assignment."DueAt" <= {tomorrow}
              AND NOT classroom."IsArchived"
              AND member."Status" = 'Active' AND learner."Status" = 'Active' AND learner."EmailConfirmed"
              AND NOT EXISTS (
                  SELECT 1 FROM "Notifications" notice
                  WHERE notice."AssignmentId" = assignment."Id" AND notice."UserId" = member."UserId"
                    AND notice."Type" = 'AssignmentDue'
              )
              AND EXISTS (
                  SELECT 1 FROM "AssignmentLessons" lesson
                  WHERE lesson."AssignmentId" = assignment."Id"
                    AND NOT EXISTS (
                        SELECT 1 FROM "AssignmentSubmissions" submission
                        JOIN "LearningSessions" session ON session."Id" = submission."SessionId"
                        WHERE submission."AssignmentId" = assignment."Id" AND submission."UserId" = member."UserId"
                          AND session."LessonId" = lesson."LessonId" AND session."Score" >= assignment."MinimumScore"
                    )
              )
            ORDER BY assignment."DueAt", assignment."Id", member."UserId"
            LIMIT 200
            ON CONFLICT ("AssignmentId", "UserId", "Type") WHERE "AssignmentId" IS NOT NULL DO NOTHING
            """, cancellationToken);
    }
}
