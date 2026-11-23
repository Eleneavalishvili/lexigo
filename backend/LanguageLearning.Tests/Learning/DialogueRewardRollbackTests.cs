using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class DialogueRewardRollbackTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Theory]
    [InlineData(4, 5, 10, 10)]
    [InlineData(3, 4, 10, 0)]
    [InlineData(4, 5, 17, 17)]
    [InlineData(4, 5, 0, 0)]
    public async Task RewardRequiresAtLeastEightyPercentCorrect(int correct, int total, int configuredXp, int expectedXp)
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await account.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, account.Id);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await ConfigureRewardAsync(database, account.Id, configuredXp);
        var dialogue = new Dialogue
        {
            CourseId = content.CourseId, Title = "Synthetic reward boundary",
            Scenario = Guid.NewGuid().ToString("N"), Status = "Published"
        };
        var nodes = Enumerable.Range(0, total + 1).Select(index => new DialogueNode
        {
            DialogueId = dialogue.Id, Text = $"Synthetic turn {index}", IsStart = index == 0,
            IsTerminal = index == total
        }).ToArray();
        database.Dialogues.Add(dialogue);
        database.DialogueNodes.AddRange(nodes);
        for (int index = 0; index < total; index++)
            database.DialogueChoices.Add(new DialogueChoice
            {
                DialogueId = dialogue.Id, NodeId = nodes[index].Id, NextNodeId = nodes[index + 1].Id,
                Text = "Continue this test path", Feedback = "Synthetic boundary feedback", IsCorrect = index < correct
            });
        await database.SaveChangesAsync();
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token))
            .EnsureSuccessStatusCode();
        var response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/dialogues/{dialogue.Id}/sessions", bearer: token);
        response.EnsureSuccessStatusCode();
        var view = (await response.Content.ReadFromJsonAsync<DialogueView>())!;
        for (int index = 0; index < total; index++)
        {
            response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/dialogue-sessions/{view.Id}/choices",
                new { choiceId = Assert.Single(view.Current.Choices).Id, idempotencyKey = Guid.NewGuid() }, token);
            response.EnsureSuccessStatusCode();
            view = (await response.Content.ReadFromJsonAsync<DialogueView>())!;
            if (index < total - 1) Assert.Equal(0, view.AwardedXp);
        }
        Assert.NotNull(view.CompletedAt);
        Assert.Equal(expectedXp, view.AwardedXp);
        Assert.Equal(expectedXp, await database.XpTransactions.Where(item => item.UserId == account.Id)
            .SumAsync(item => item.Amount));
    }

    [Fact]
    public async Task FailedRewardRollsBackFinalChoiceAndSameRequestCanRetryOnce()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await account.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, account.Id);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await ConfigureRewardAsync(database, account.Id, 10);
        await scope.ServiceProvider.GetRequiredService<StarterDialogueInstaller>().InstallAsync(CancellationToken.None);
        var dialogue = await database.Dialogues.SingleAsync(item => item.CourseId == content.CourseId && item.Scenario == "cafe");
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token))
            .EnsureSuccessStatusCode();
        var startResponse = await browser.SendAsync(HttpMethod.Post,
            $"/api/v1/dialogues/{dialogue.Id}/sessions", bearer: token);
        startResponse.EnsureSuccessStatusCode();
        var session = (await startResponse.Content.ReadFromJsonAsync<DialogueView>())!;
        var opening = await database.DialogueChoices.Where(item => item.NodeId == session.Current.Id && item.IsCorrect)
            .OrderBy(item => item.Id).FirstAsync();
        string route = $"/api/v1/me/dialogue-sessions/{session.Id}/choices";
        var first = await browser.SendAsync(HttpMethod.Post, route,
            new { choiceId = opening.Id, idempotencyKey = Guid.NewGuid() }, token);
        first.EnsureSuccessStatusCode();
        var followUp = (await first.Content.ReadFromJsonAsync<DialogueView>())!;
        Assert.Null(followUp.CompletedAt);
        Assert.Equal(0, followUp.AwardedXp);
        var choice = Assert.Single(followUp.Current.Choices);
        var finalRequest = new { choiceId = choice.Id, idempotencyKey = Guid.NewGuid() };

        await database.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_fail_dialogue_reward() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Synthetic reward persistence failure'; END; $body$;
            CREATE TRIGGER test_fail_dialogue_reward BEFORE INSERT ON "XpTransactions"
            FOR EACH ROW WHEN (NEW."SourceType" = 'FirstDialogueCompletion')
            EXECUTE FUNCTION test_fail_dialogue_reward();
            """);
        try
        {
            var failed = await browser.SendAsync(HttpMethod.Post, route, finalRequest, token);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.DoesNotContain("Synthetic reward", await failed.Content.ReadAsStringAsync());
            Assert.Null(await database.DialogueSessions.Where(item => item.Id == session.Id)
                .Select(item => item.CompletedAt).SingleAsync());
            Assert.Equal(1, await database.DialogueAttempts.CountAsync(item => item.SessionId == session.Id));
            Assert.False(await database.XpTransactions.AnyAsync(item => item.UserId == account.Id));
            Assert.False(await database.DailyActivities.AnyAsync(item => item.UserId == account.Id));
        }
        finally
        {
            await database.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER test_fail_dialogue_reward ON "XpTransactions";
                DROP FUNCTION test_fail_dialogue_reward();
                """);
        }

        var retry = await browser.SendAsync(HttpMethod.Post, route, finalRequest, token);
        retry.EnsureSuccessStatusCode();
        Assert.Equal(10, (await retry.Content.ReadFromJsonAsync<DialogueView>())!.AwardedXp);
        var repeated = await browser.SendAsync(HttpMethod.Post, route, finalRequest, token);
        repeated.EnsureSuccessStatusCode();
        Assert.Equal(10, (await repeated.Content.ReadFromJsonAsync<DialogueView>())!.AwardedXp);
        Assert.Equal(1, await database.XpTransactions.CountAsync(item => item.UserId == account.Id));
        Assert.Equal(2, await database.DialogueAttempts.CountAsync(item => item.SessionId == session.Id));
    }

    [Fact]
    public async Task LegacyStoredSettingsUseTheDocumentedConversationDefault()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await ConfigureRewardAsync(database, account.Id, 10);
        await database.SystemSettings.Where(item => item.Key == "study-defaults").ExecuteUpdateAsync(update =>
            update.SetProperty(item => item.Value,
                "{\"LessonXp\":20,\"DailyGoalXp\":20,\"AudioUploadMiB\":10,\"StudyDays\":31,\"ReminderTime\":\"18:00:00\"}"));
        var restored = await scope.ServiceProvider.GetRequiredService<SystemSettings>().ReadAsync(CancellationToken.None);
        Assert.Equal(10, restored.Defaults.DialogueXp);
    }

    private static async Task ConfigureRewardAsync(LexiGoDbContext database, Guid actorId, int amount)
    {
        var setting = await database.SystemSettings.SingleOrDefaultAsync(item => item.Key == "study-defaults");
        if (setting is null)
        {
            setting = new SystemSetting { Key = "study-defaults", Value = "{}", UpdatedBy = actorId };
            database.SystemSettings.Add(setting);
        }
        setting.Value = System.Text.Json.JsonSerializer.Serialize(StudyDefaults.Standard with { DialogueXp = amount });
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync();
    }
}
