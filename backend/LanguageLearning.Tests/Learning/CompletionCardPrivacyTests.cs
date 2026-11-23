using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed partial class CompletionCardTests
{
    [Fact]
    public async Task EveryPublishedLessonIncludingTheLastUnitStepMustBeCompleted()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(browser);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var lesson = new Lesson { UnitId = setup.Unit, OwnerId = setup.Account.Id, Title = "Unit check", Objective = "Review", OrderNumber = 2, Status = "Published" };
        var version = new LessonVersion
        {
            LessonId = lesson.Id,
            CreatedBy = setup.Account.Id,
            VersionNumber = 1,
            Title = lesson.Title,
            Objective = lesson.Objective,
            Explanation = "Review fixture",
            Status = "Published",
            PublishedAt = DateTimeOffset.UtcNow
        };
        var exercise = new Exercise { LessonVersionId = version.Id, Type = "Translation", Prompt = "Translate hello", Explanation = "Hola is a greeting.", OrderNumber = 1 };
        db.AddRange(lesson, version, exercise, new AcceptedAnswer { ExerciseId = exercise.Id, AnswerText = "hola" });
        await db.SaveChangesAsync();
        lesson.CurrentVersionId = version.Id;
        await db.SaveChangesAsync();
        await FinishAsync(browser, setup.Token, setup.Content.LessonId);
        Assert.False(await db.CompletionCards.AnyAsync(item => item.UserId == setup.Account.Id));
        await FinishAsync(browser, setup.Token, lesson.Id);
        var card = await CardAsync(browser, setup.Token, setup.Unit);
        Assert.Equal(2, card.Details.LessonCount);
        Assert.Equal(2, card.Details.GradedLessonCount);
    }

    [Fact]
    public async Task ExportIncludesOwnedCardWithoutSharingCapabilityAndDeletionRevokesIt()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(browser);
        await FinishAsync(browser, setup.Token, setup.Content.LessonId);
        var card = await CardAsync(browser, setup.Token, setup.Unit);
        (await ShareAsync(browser, setup.Token, card, true)).EnsureSuccessStatusCode();
        var shared = await CardAsync(browser, setup.Token, setup.Unit);
        var exported = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/export", bearer: setup.Token);
        exported.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await exported.Content.ReadAsStringAsync());
        var exportedCard = Assert.Single(document.RootElement.GetProperty("completionCards").EnumerateArray());
        Assert.Equal(card.Id, exportedCard.GetProperty("id").GetGuid());
        Assert.False(exportedCard.TryGetProperty("shareCode", out _));
        var deleted = await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password,
            confirmation = "DELETE",
            recoveryCode = setup.Account.RecoveryCodes[1]
        }, setup.Token, csrf: true);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/api/v1/completion-cards/shared/" + shared.ShareCode)).StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.False(await db.CompletionCards.AnyAsync(item => item.UserId == setup.Account.Id));
        db.CompletionCards.Add(new CompletionCard
        {
            UserId = setup.Account.Id,
            CourseId = card.CourseId,
            UnitId = card.UnitId,
            CourseTitle = card.Details.CourseTitle,
            UnitTitle = card.Details.UnitTitle,
            IssuedAt = card.Details.IssuedAt,
            CompletedAt = card.Details.CompletedAt,
            VerificationCode = Guid.NewGuid().ToString("N"),
            LessonCount = 1
        });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task DatabaseProtectsResultSnapshotsDuplicatesAndSharingRevision()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(browser);
        await FinishAsync(browser, setup.Token, setup.Content.LessonId);
        var card = await CardAsync(browser, setup.Token, setup.Unit);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"CompletionCards\" SET \"UnitTitle\" = 'Tampered' WHERE \"Id\" = {card.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, immutable.SqlState);
        string shareCode = Guid.NewGuid().ToString("N");
        var revision = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"CompletionCards\" SET \"IsShared\" = true, \"ShareCode\" = {shareCode} WHERE \"Id\" = {card.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, revision.SqlState);
        var copy = new CompletionCard
        {
            UserId = setup.Account.Id,
            CourseId = card.CourseId,
            UnitId = card.UnitId,
            CourseTitle = card.Details.CourseTitle,
            UnitTitle = card.Details.UnitTitle,
            IssuedAt = card.Details.IssuedAt,
            CompletedAt = card.Details.CompletedAt,
            VerificationCode = Guid.NewGuid().ToString("N"),
            LessonCount = 1,
            GradedLessonCount = 1
        };
        db.CompletionCards.Add(copy);
        var invalidScore = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(invalidScore.InnerException).SqlState);
        db.ChangeTracker.Clear();
        copy.AverageBestScore = 100;
        db.CompletionCards.Add(copy);
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
    }

    [Fact]
    public async Task MigrationRollbackRefusesToDiscardEarnedCards()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(browser);
        await FinishAsync(browser, setup.Token, setup.Content.LessonId);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var cardMigration = applied.Single(item => item.EndsWith("_AddCompletionCards", StringComparison.Ordinal));
        var previousMigration = applied[Array.IndexOf(applied, cardMigration) - 1];
        // Exercise this migration's guard without rolling back unrelated newer features.
        var rollback = db.GetService<IMigrator>().GenerateScript(cardMigration, previousMigration,
            MigrationsSqlGenerationOptions.NoTransactions);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(rollback));
        await transaction.RollbackAsync();
        Assert.Contains("explicitly migrated", exception.MessageText);
        Assert.Contains(cardMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.True(await db.CompletionCards.AnyAsync(item => item.UserId == setup.Account.Id));
    }
}
