using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Api.Vocabulary;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Infrastructure.Vocabulary;
using LanguageLearning.Tests.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Vocabulary;

public sealed partial class VocabularyRevisionTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string Path = "/api/v1/manage/vocabulary/revisions";
    private static readonly Guid Course = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Word = Guid.Parse("15110000-0000-0000-0000-000000000001");

    [Fact]
    public async Task WithdrawalPreservesThePublishedWordAndAllowsAuthorDeletion()
    {
        Guid courseId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid wordId = Guid.Parse("15110000-0000-0000-0000-000000000007");
        using var writer = Browser();
        using var outsider = Browser();
        using var reviewer = Browser();
        using var admin = Browser();
        var owner = await SecurityTestAccount.CreateAsync(fixture);
        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(owner.Id.ToString()))!, RoleNames.ContentAuthor)).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            db.UserLanguageScopes.Add(new UserLanguageScope { UserId = owner.Id, LanguageId = 3 });
            await db.SaveChangesAsync();
        }
        string token = await owner.LoginAsync(writer);
        string foreignToken = await StaffAsync(outsider, 3, RoleNames.ContentAuthor);
        string reviewerToken = await StaffAsync(reviewer, 3, RoleNames.Reviewer);
        string adminToken = await StaffAsync(admin, 3, RoleNames.Admin);
        var input = new CreateVocabularyRevision(courseId, wordId,
            new VocabularyText("ciao", "hi", "Ciao, Anna.", "interjection", "greetings"));
        var created = await writer.SendAsync(HttpMethod.Post, Path, input, token);
        created.EnsureSuccessStatusCode();
        var draft = Assert.Single(await List(writer, token));
        Assert.Equal(HttpStatusCode.Forbidden, await Change(outsider, foreignToken, draft, "withdraw"));
        Assert.Equal(HttpStatusCode.OK, await Change(writer, token, draft, "withdraw"));
        Assert.Equal(HttpStatusCode.OK, await Change(writer, token, draft, "withdraw"));
        Assert.Empty(await List(reviewer, reviewerToken));

        (await writer.SendAsync(HttpMethod.Post, Path, input, token)).EnsureSuccessStatusCode();
        var replacement = (await List(writer, token)).Single(item => item.Status == "Draft");
        Assert.Equal(HttpStatusCode.OK, await Change(writer, token, replacement, "submit"));
        Assert.Equal(HttpStatusCode.Conflict, await Change(writer, token, replacement, "withdraw"));
        var submitted = Assert.Single(await List(reviewer, reviewerToken));
        Assert.Equal(HttpStatusCode.OK, await Change(admin, adminToken, submitted, "withdraw"));

        var racingInput = input with { Text = input.Text with { EnglishTranslation = "hello" } };
        (await writer.SendAsync(HttpMethod.Post, Path, racingInput, token)).EnsureSuccessStatusCode();
        var racingDraft = (await List(writer, token)).Single(item => item.Status == "Draft");
        Assert.Equal(HttpStatusCode.OK, await Change(writer, token, racingDraft, "submit"));
        var racingSubmission = Assert.Single(await List(reviewer, reviewerToken));
        Assert.Equal(HttpStatusCode.OK, await Change(reviewer, reviewerToken, racingSubmission, "approve"));
        var approved = (await List(admin, adminToken)).Single(item => item.Status == "Approved");
        var race = await Task.WhenAll(
            Change(writer, token, approved, "withdraw"),
            Change(admin, adminToken, approved, "publish"));
        Assert.Single(race, status => status == HttpStatusCode.OK);
        Assert.Single(race, status => status == HttpStatusCode.Conflict);

        var deleted = await writer.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password, confirmation = "DELETE", recoveryCode = owner.RecoveryCodes[1]
        }, token, csrf: true);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var finalScope = fixture.Services.CreateScope();
        var database = finalScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.False(await database.VocabularyRevisions.AnyAsync(item => item.AuthorId == owner.Id && item.Status != "Published"));
        Assert.Equal("hello", await database.VocabularyItems.Where(item => item.Id == wordId)
            .Select(item => item.EnglishTranslation).SingleAsync());
        (await outsider.SendAsync(HttpMethod.Post, Path, input, foreignToken)).EnsureSuccessStatusCode();
        var retained = Assert.Single(await List(outsider, foreignToken));
        Assert.Equal(HttpStatusCode.OK, await Change(outsider, foreignToken, retained, "withdraw"));
        var migrator = database.GetService<IMigrator>();
        try
        {
            var rollback = await Assert.ThrowsAsync<PostgresException>(() =>
                migrator.MigrateAsync("20261105203000_GuardDeletedAccountWrites"));
            Assert.Contains("withdrawn revision history", rollback.MessageText);
        }
        finally
        {
            // Earlier rollback steps can commit before the guarded migration refuses to roll back.
            await migrator.MigrateAsync();
        }
        Assert.True(await database.VocabularyRevisions.AnyAsync(item => item.Id == retained.Id && item.Status == "Withdrawn"));

        async Task<VocabularyRevisionView[]> List(SessionTestBrowser browser, string bearer)
        {
            var response = await browser.SendAsync(HttpMethod.Get, $"{Path}?courseId={courseId}", bearer: bearer);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<VocabularyRevisionView[]>())!;
        }
        async Task<HttpStatusCode> Change(SessionTestBrowser browser, string bearer, VocabularyRevisionView revision, string action)
        {
            return (await browser.SendAsync(HttpMethod.Put, $"{Path}/{revision.Id}",
                new ChangeVocabularyRevision(revision.RevisionToken, action, null,
                    action == "approve" ? "Reviewed wording." : null), bearer)).StatusCode;
        }
    }

    [Fact]
    public async Task OnlyIndependentlyApprovedRevisionChangesPublishedVocabulary()
    {
        using var writer = Browser();
        using var reader = Browser();
        using var foreign = Browser();
        using var admin = Browser();
        using var learner = Browser();
        string authorToken = await StaffAsync(writer, 2, RoleNames.ContentAuthor, RoleNames.Reviewer);
        string reviewerToken = await StaffAsync(reader, 2, RoleNames.Reviewer);
        string foreignToken = await StaffAsync(foreign, 3, RoleNames.Reviewer);
        string adminToken = await StaffAsync(admin, 2, RoleNames.Admin);
        var student = await SecurityTestAccount.CreateAsync(fixture);
        string studentToken = await student.LoginAsync(learner);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await database.Courses.Where(item => item.Id == Course)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsPublished, true));
        var starterWords = await database.VocabularyItems.AsNoTracking()
            .Where(item => item.Id.ToString().StartsWith("15110000-"))
            .ToArrayAsync();
        Assert.Equal(24, starterWords.Length);
        Assert.Equal(4, starterWords.GroupBy(item => item.CourseId).Count());
        Assert.All(starterWords, item => Assert.False(string.IsNullOrWhiteSpace(item.Example)));

        var saved = await learner.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words",
            new { vocabularyItemId = Word }, studentToken);
        saved.EnsureSuccessStatusCode();
        var input = new CreateVocabularyRevision(Course, Word,
            new VocabularyText("hola", "hi", "Hola, Ana.", "interjection", "greetings"));
        var create = await writer.SendAsync(HttpMethod.Post, Path, input, authorToken);
        create.EnsureSuccessStatusCode();
        Guid id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await writer.SendAsync(HttpMethod.Post, Path, input, authorToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.SendAsync(HttpMethod.Get,
            $"{Path}?courseId={Course}", bearer: studentToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await foreign.SendAsync(HttpMethod.Get,
            $"{Path}?courseId={Course}", bearer: foreignToken)).StatusCode);
        Assert.Empty(await ListAsync(reader, reviewerToken));

        var draft = Assert.Single(await ListAsync(writer, authorToken));
        Assert.Equal(id, draft.Id);
        Assert.Equal(HttpStatusCode.Conflict, await Change(admin, adminToken, draft, "publish"));
        Assert.Equal(HttpStatusCode.OK, await Change(writer, authorToken, draft, "submit"));
        var submitted = Assert.Single(await ListAsync(reader, reviewerToken));
        Assert.Equal(HttpStatusCode.Forbidden, await Change(writer, authorToken, submitted, "approve"));
        Assert.Equal(HttpStatusCode.Forbidden, await Change(foreign, foreignToken, submitted, "approve"));
        Assert.Equal(HttpStatusCode.OK, await Change(reader, reviewerToken, submitted, "request-changes"));
        var returned = Assert.Single(await ListAsync(writer, authorToken));
        Assert.Equal("Draft", returned.Status);
        Assert.Equal(HttpStatusCode.OK, await Change(writer, authorToken, returned, "submit"));
        var resubmitted = Assert.Single(await ListAsync(reader, reviewerToken));
        Assert.Equal(HttpStatusCode.Conflict, await Change(reader, reviewerToken, submitted, "approve"));
        Assert.Equal(HttpStatusCode.OK, await Change(reader, reviewerToken, resubmitted, "approve"));
        var approved = Assert.Single(await ListAsync(admin, adminToken));
        Assert.Equal("hello", (await SavedWordAsync()).EnglishTranslation);
        Assert.Equal(HttpStatusCode.OK, await Change(admin, adminToken, approved, "publish"));
        Assert.Equal(HttpStatusCode.OK, await Change(admin, adminToken, approved, "publish"));
        var published = Assert.Single(await ListAsync(admin, adminToken));
        Assert.Equal("Published", published.Status);
        Assert.Equal(HttpStatusCode.Conflict, await Change(admin, adminToken, published, "withdraw"));
        Assert.NotNull(published.ReviewerId);
        Assert.NotNull(published.PublishedAt);
        Assert.Equal(Word, (await SavedWordAsync()).Id);
        Assert.Equal("hi", (await SavedWordAsync()).EnglishTranslation);

        var newWord = new VocabularyText("la montaña", "mountain", "La montaña está cerca.", "noun", "travel");
        var newDraftResponse = await writer.SendAsync(HttpMethod.Post, Path,
            new CreateVocabularyRevision(Course, null, newWord), authorToken);
        newDraftResponse.EnsureSuccessStatusCode();
        id = (await newDraftResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var newDraft = (await ListAsync(writer, authorToken)).Single(item => item.Id == id);
        Assert.Empty(await FindMountainAsync());
        Assert.Equal(HttpStatusCode.OK, await Change(writer, authorToken, newDraft, "submit"));
        var newSubmission = (await ListAsync(reader, reviewerToken)).Single(item => item.Id == id);
        Assert.Equal(HttpStatusCode.OK, await Change(reader, reviewerToken, newSubmission, "approve"));
        Assert.Empty(await FindMountainAsync());
        var newApproval = (await ListAsync(admin, adminToken)).Single(item => item.Id == id);
        Assert.Equal(HttpStatusCode.OK, await Change(admin, adminToken, newApproval, "publish"));
        var mountain = Assert.Single(await FindMountainAsync());
        Assert.Equal(newWord.Example, mountain.Example);
        Assert.Equal("travel", mountain.Topic);
        var saveMountain = await learner.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words",
            new { vocabularyItemId = mountain.Id }, studentToken);
        saveMountain.EnsureSuccessStatusCode();

        async Task<WordView[]> FindMountainAsync()
        {
            var response = await learner.SendAsync(HttpMethod.Get,
                $"/api/v1/vocabulary?courseId={Course}&search=mountain&topic=travel", bearer: studentToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<WordPage>())!.Items;
        }

        async Task<HttpStatusCode> Change(SessionTestBrowser browser, string token, VocabularyRevisionView revision, string action)
        {
            var response = await browser.SendAsync(HttpMethod.Put, $"{Path}/{id}",
                new ChangeVocabularyRevision(revision.RevisionToken, action, null, "Reviewed in test."), token);
            return response.StatusCode;
        }

        async Task<WordView> SavedWordAsync()
        {
            var response = await learner.SendAsync(HttpMethod.Get, "/api/v1/me/saved-words", bearer: studentToken);
            response.EnsureSuccessStatusCode();
            return Assert.Single((await response.Content.ReadFromJsonAsync<WordPage>())!.Items);
        }
    }

    private SessionTestBrowser Browser() => new(fixture.CreateIsolatedClient());

    private async Task<VocabularyRevisionView[]> ListAsync(SessionTestBrowser browser, string token)
    {
        var response = await browser.SendAsync(HttpMethod.Get, $"{Path}?courseId={Course}", bearer: token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VocabularyRevisionView[]>())!;
    }

    private async Task<string> StaffAsync(SessionTestBrowser browser, int languageId, params string[] roles)
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(account.Id.ToString()))!;
        Assert.True((await users.AddToRolesAsync(user, roles)).Succeeded);
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        database.UserLanguageScopes.Add(new UserLanguageScope { UserId = account.Id, LanguageId = languageId });
        await database.SaveChangesAsync();
        return await account.LoginAsync(browser);
    }
}
