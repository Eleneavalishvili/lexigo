using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests;

public sealed class DocumentedJsonStorageTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task PostgreSqlUsesJsonbOnlyForReviewedPayloadsSnapshotsAndTheTwoDocumentedExceptions()
    {
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var columns = await database.Database.SqlQueryRaw<string>("""
            SELECT (table_name || '.' || column_name)::text AS "Value"
            FROM information_schema.columns WHERE table_schema = 'public' AND udt_name = 'jsonb'
            ORDER BY table_name, column_name
            """).ToArrayAsync();
        // The user retained the explicit PDF model fields on 2026-11-20.
        string[] expected = [
            "AcceptedAnswers.OrderedTokenIds", "AnswerAttempts.AnswerPayload", "AnswerAttempts.ResponseSnapshot",
            "BillingOperations.RequestSnapshot", "BillingOperations.ResponseSnapshot", "DialogueSessions.VersionSnapshot",
            "Exercises.EvaluationRules", "ImportJobs.Payload", "ImportJobs.ValidationErrors", "LearningSessions.PlacementMap",
            "ReminderPreferences.WeekDays", "SessionExercises.PrivateEvaluationSnapshot", "SessionExercises.PublicSnapshot",
            "SpeechCheckAttempts.ResponseSnapshot", "SystemSettings.Value"
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), columns.Order(StringComparer.Ordinal));
    }
}
