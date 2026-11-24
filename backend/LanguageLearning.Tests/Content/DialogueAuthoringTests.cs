using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Tests.Accounts;
using System.IdentityModel.Tokens.Jwt;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task DialogueMigrationCannotDiscardStaffOwnership()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var created = await author.SendAsync(HttpMethod.Post, "/api/v1/manage/dialogues",
            new CreateDialogueInput(Spanish, "rollback-" + Guid.NewGuid().ToString("N")[..12], "Retain ownership", false), writer.Token);
        var id = await IdAsync(created, HttpStatusCode.Created, "/api/v1/manage/dialogues");
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var script = database.GetService<IMigrator>().GenerateScript("20261124134822_AddDialogueAuthoring",
            "20261120085844_AddSkillManagement", MigrationsSqlGenerationOptions.NoTransactions);
        await using var transaction = await database.Database.BeginTransactionAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync(script));
        await transaction.RollbackAsync();
        Assert.Contains("Retain dialogue authorship", error.MessageText);
        Assert.Equal(writer.Account.Id, (await database.Dialogues.SingleAsync(item => item.Id == id)).OwnerId);
    }
    [Fact]
    public async Task DeletedAuthorErasesArchivedPrivateDialogueTextButKeepsRelationalHistory()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var created = await author.SendAsync(HttpMethod.Post, "/api/v1/manage/dialogues",
            new CreateDialogueInput(Spanish, "private-" + Guid.NewGuid().ToString("N")[..12], "Private note", false), writer.Token);
        var id = await IdAsync(created, HttpStatusCode.Created, "/api/v1/manage/dialogues");
        var draft = await ReadDialogueAsync(author, writer.Token, id);
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/dialogues/{id}",
            new DialogueDraftInput(draft.Dialogue.EditToken, "Private note", false, DialogueGraph()), writer.Token)).EnsureSuccessStatusCode();
        var family = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(writer.Token).Claims.Single(claim => claim.Type == "sid").Value);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var pending = await scope.ServiceProvider.GetRequiredService<AccountDeletionResponsibilities>()
                .ForSessionAsync(writer.Account.Id, family, default);
            Assert.Contains(pending!, item => item.Code == "dialogue-drafts");
        }
        draft = await ReadDialogueAsync(author, writer.Token, id);
        using var archived = await author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/dialogues/{id}",
            new DialogueTransition(draft.Dialogue.EditToken, "Withdraw this unpublished material."), writer.Token);
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var error = await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().DeleteAsync(
                writer.Account.Id, family, SecurityTestAccount.Password, null, writer.Account.RecoveryCodes[1], "test-dialogue-erasure", default);
            Assert.Null(error);
        }
        await using var verification = fixture.Services.CreateAsyncScope();
        var database = verification.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var row = await database.Dialogues.SingleAsync(item => item.Id == id);
        Assert.Equal("Archived dialogue", row.Title);
        Assert.Equal("gone" + id, row.Scenario);
        Assert.All(await database.DialogueNodes.Where(node => node.DialogueId == id).ToArrayAsync(), node => Assert.Equal("Private note erased after account deletion.", node.Text));
        Assert.Equal(writer.Account.Id, row.OwnerId);
    }

    private static DialogueNodeInput[] DialogueGraph() =>
    [
        new("start", "¡Hola!", true, false,
            [new("Hola.", "A polite greeting.", true, "end"), new("Adiós.", "Use a greeting when arriving.", false, "end")]),
        new("end", "¡Bienvenido!", false, true, [])
    ];

    private static async Task<StaffDialogue> ReadDialogueAsync(LanguageLearning.Tests.Accounts.SessionTestBrowser browser, string token, Guid id)
    {
        using var response = await browser.SendAsync(HttpMethod.Get, $"/api/v1/manage/dialogues/{id}", bearer: token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StaffDialogue>())!;
    }

    [Fact]
    public async Task DialogueRequiresIndependentReviewAndRevisionPreservesPublishedNodes()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser();
        var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var created = await author.SendAsync(HttpMethod.Post, "/api/v1/manage/dialogues",
            new CreateDialogueInput(Spanish, "visit-" + Guid.NewGuid().ToString("N")[..12], "A first visit", false), writer.Token);
        var id = await IdAsync(created, HttpStatusCode.Created, "/api/v1/manage/dialogues");
        var detail = await ReadDialogueAsync(author, writer.Token, id);
        var input = new DialogueDraftInput(detail.Dialogue.EditToken, "A polite visit", false, DialogueGraph());
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/dialogues/{id}", input, writer.Token)).EnsureSuccessStatusCode();
        using var privateRead = await reviewer.SendAsync(HttpMethod.Get, $"/api/v1/manage/dialogues/{id}", bearer: reader.Token);
        Assert.Equal(HttpStatusCode.NotFound, privateRead.StatusCode);
        detail = await ReadDialogueAsync(author, writer.Token, id);
        var transition = new DialogueTransition(detail.Dialogue.EditToken, "Ready for independent language review.");
        using var earlyPublish = await admin.SendAsync(HttpMethod.Post, $"/api/v1/manage/dialogues/{id}/publish", transition, administrator.Token);
        Assert.Equal(HttpStatusCode.Conflict, earlyPublish.StatusCode);
        (await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/dialogues/{id}/submit", transition, writer.Token)).EnsureSuccessStatusCode();
        detail = await ReadDialogueAsync(reviewer, reader.Token, id);
        transition = transition with { EditToken = detail.Dialogue.EditToken };
        await using (var roleScope = fixture.Services.CreateAsyncScope())
        {
            var users = roleScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(writer.Account.Id.ToString()))!, RoleNames.Reviewer)).Succeeded);
        }
        // A role change invalidates the previous role snapshot. Reauthenticate with MFA
        // before checking that a dual-role author still cannot approve their own work.
        writer = (writer.Account, await writer.Account.LoginAsync(author, 1));
        using var selfReview = await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/dialogues/{id}/approve", transition, writer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, selfReview.StatusCode);
        (await reviewer.SendAsync(HttpMethod.Post, $"/api/v1/manage/dialogues/{id}/approve", transition, reader.Token)).EnsureSuccessStatusCode();
        detail = await ReadDialogueAsync(admin, administrator.Token, id);
        (await admin.SendAsync(HttpMethod.Post, $"/api/v1/manage/dialogues/{id}/publish",
            transition with { EditToken = detail.Dialogue.EditToken }, administrator.Token)).EnsureSuccessStatusCode();
        detail = await ReadDialogueAsync(author, writer.Token, id);
        Assert.Equal("Published", detail.Dialogue.Status);
        var originalNodes = detail.Nodes.Select(node => node.Key).Order().ToArray();
        using var revision = await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/dialogues/{id}/versions", bearer: writer.Token);
        var revisionId = await IdAsync(revision, HttpStatusCode.Created, "/api/v1/manage/dialogues");
        var copy = await ReadDialogueAsync(author, writer.Token, revisionId);
        Assert.Equal(2, copy.Dialogue.VersionNumber);
        Assert.Equal("Draft", copy.Dialogue.Status);
        Assert.Null(copy.ReviewerId);
        Assert.DoesNotContain(copy.Nodes, node => originalNodes.Contains(node.Key));
        var original = await ReadDialogueAsync(author, writer.Token, id);
        Assert.Equal(originalNodes, original.Nodes.Select(node => node.Key).Order());
        using var immutable = await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/dialogues/{id}",
            input with { EditToken = original.Dialogue.EditToken }, writer.Token);
        Assert.Equal(HttpStatusCode.Conflict, immutable.StatusCode);
    }

    [Fact]
    public async Task DialogueRejectsCyclesStaleEditsForeignAuthorsAndUnreviewedAudio()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var stranger = Browser();
        var other = await StaffAsync(stranger, RoleNames.ContentAuthor);
        using var created = await author.SendAsync(HttpMethod.Post, "/api/v1/manage/dialogues",
            new CreateDialogueInput(Spanish, "safe-" + Guid.NewGuid().ToString("N")[..12], "Safe graph", false), writer.Token);
        var id = await IdAsync(created, HttpStatusCode.Created, "/api/v1/manage/dialogues");
        var detail = await ReadDialogueAsync(author, writer.Token, id);
        var input = new DialogueDraftInput(detail.Dialogue.EditToken, "Safe graph", false, DialogueGraph());
        using var foreign = await stranger.SendAsync(HttpMethod.Put, $"/api/v1/manage/dialogues/{id}", input, other.Token);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        var cycle = DialogueGraph();
        cycle[0] = cycle[0] with { Choices = [new("Loop", "Invalid self-loop", true, "start")] };
        using var invalid = await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/dialogues/{id}", input with { Nodes = cycle }, writer.Token);
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        var audio = DialogueGraph();
        audio[0] = audio[0] with { AudioMediaId = Guid.NewGuid() };
        using var invalidAudio = await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/dialogues/{id}", input with { Nodes = audio }, writer.Token);
        Assert.Equal(HttpStatusCode.Conflict, invalidAudio.StatusCode);
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/dialogues/{id}", input, writer.Token)).EnsureSuccessStatusCode();
        using var stale = await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/dialogues/{id}", input, writer.Token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(2, await database.DialogueNodes.CountAsync(node => node.DialogueId == id));
        Assert.Equal(1, await database.AuditLogs.CountAsync(row => row.EntityId == id.ToString() && row.Action == "DialogueDraftEdited"));
    }
}
