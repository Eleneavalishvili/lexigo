using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LanguageLearning.Infrastructure.Data.Migrations;

[DbContext(typeof(LexiGoDbContext))]
[Migration("20261105203000_GuardDeletedAccountWrites")]
public sealed class GuardDeletedAccountWrites : Migration
{
    private static readonly (string Table, string Owner)[] Tables =
    [
        ("UserPreferences", "UserId"), ("ReminderPreferences", "UserId"),
        ("SavedWords", "UserId"), ("WordCollections", "UserId"), ("ClassMemberships", "UserId"),
        ("AssignmentSubmissions", "UserId"), ("SpeechCheckAttempts", "UserId"),
        ("AnswerAttempts", "UserId"), ("SessionExercises", "UserId"), ("LearningSessions", "UserId"),
        ("LessonProgress", "UserId"), ("UserAchievements", "UserId"), ("UserChallenges", "UserId"),
        ("LearnerHearts", "UserId"), ("Enrollments", "UserId"), ("ReminderDeliveries", "UserId"), ("EmailOutboxMessages", "UserId"),
        ("Notifications", "UserId"), ("ContentReports", "ReporterId"), ("ImportJobs", "AuthorId"),
        ("ExtraStudyRequests", "LearnerId"), ("ExtraStudyOffers", "TeacherId"),
        ("Classrooms", "TeacherId"), ("TeacherFeedback", "TeacherId"),
        ("VocabularyRevisions", "AuthorId"), ("Lessons", "OwnerId"), ("LessonVersions", "CreatedBy")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE FUNCTION lexigo_guard_deleted_account() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE owner_status text;
            BEGIN
                IF TG_OP = 'UPDATE' AND TG_TABLE_NAME = 'EmailOutboxMessages' AND
                    to_jsonb(NEW)->>'Recipient' = '' AND to_jsonb(NEW)->>'HtmlBody' = '' AND
                    (to_jsonb(NEW)->>'Attempts')::int = 8 THEN
                    RETURN NEW;
                END IF;
                SELECT "Status" INTO owner_status FROM "Users"
                WHERE "Id" = (to_jsonb(NEW)->>TG_ARGV[0])::uuid FOR SHARE;
                IF owner_status = 'Deleted' THEN
                    RAISE EXCEPTION 'Deleted account cannot receive private data' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $body$;
            """);
        foreach (var (table, owner) in Tables)
            migrationBuilder.Sql($"""
                CREATE TRIGGER guard_deleted_account BEFORE INSERT OR UPDATE ON "{table}"
                FOR EACH ROW EXECUTE FUNCTION lexigo_guard_deleted_account('{owner}');
                """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, _) in Tables)
            migrationBuilder.Sql($"""DROP TRIGGER guard_deleted_account ON "{table}";""");
        migrationBuilder.Sql("DROP FUNCTION lexigo_guard_deleted_account();");
    }
}
