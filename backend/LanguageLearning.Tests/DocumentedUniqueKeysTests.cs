using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests;

public sealed class DocumentedUniqueKeysTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Theory]
    [InlineData("Users", "NormalizedEmail")]
    [InlineData("Languages", "Code")]
    [InlineData("Courses", "SourceLanguageId,TargetLanguageId")]
    [InlineData("AspNetUserRoles", "UserId,RoleId")]
    [InlineData("Enrollments", "UserId,CourseId")]
    [InlineData("LessonProgress", "UserId,LessonId")]
    [InlineData("UserAchievements", "UserId,AchievementId")]
    [InlineData("ClassMemberships", "ClassroomId,UserId")]
    [InlineData("SavedWords", "UserId,VocabularyItemId")]
    [InlineData("DailyActivities", "UserId,LocalDate")]
    [InlineData("Units", "CourseId,OrderNumber")]
    [InlineData("Lessons", "UnitId,OrderNumber")]
    [InlineData("Exercises", "LessonVersionId,OrderNumber")]
    [InlineData("XpTransactions", "UserId,SourceType,SourceId")]
    [InlineData("AnswerAttempts", "UserId,IdempotencyKey")]
    public async Task MigratedPostgresEnforcesDocumentedUniqueness(string table, string expectedColumns)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var keys = await database.Database.SqlQuery<string>($"""
            SELECT string_agg(column_definition.attname::text, ',' ORDER BY key_column.position) AS "Value"
            FROM pg_index index_definition
            JOIN pg_class table_definition ON table_definition.oid = index_definition.indrelid
            JOIN pg_namespace table_schema ON table_schema.oid = table_definition.relnamespace
            CROSS JOIN LATERAL unnest(index_definition.indkey) WITH ORDINALITY AS key_column(number, position)
            JOIN pg_attribute column_definition ON column_definition.attrelid = table_definition.oid
                AND column_definition.attnum = key_column.number
            WHERE table_schema.nspname = 'public' AND table_definition.relname = {table}
                AND index_definition.indisunique AND index_definition.indisvalid
                AND index_definition.indisready
                AND index_definition.indpred IS NULL AND index_definition.indexprs IS NULL
                AND key_column.position <= index_definition.indnkeyatts
            GROUP BY index_definition.indexrelid
            """).ToArrayAsync();

        Assert.Contains(expectedColumns, keys);
    }
}
