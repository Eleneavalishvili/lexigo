using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task ManagedSkillsHavePrivateLocationsScopedReadsAndBoundedSearchPages()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var german = Browser();
        var foreign = await StaffAsync(german, RoleNames.Reviewer, 4);
        string prefix = "Skill-" + Guid.NewGuid().ToString("N");
        var first = await CreateSkillAsync(admin, administrator.Token, prefix + " one");
        var second = await CreateSkillAsync(admin, administrator.Token, prefix + " two");
        var third = await CreateSkillAsync(admin, administrator.Token, prefix + " three");
        string url = $"/api/v1/manage/skills?courseId={Spanish}&search={prefix}&pageSize=2";
        var page = await (await author.SendAsync(HttpMethod.Get, url, bearer: writer.Token))
            .Content.ReadFromJsonAsync<ManagedSkillPage>();
        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(1, page.Page);
        Assert.Equal(2, page.Items.Length);
        var next = await (await author.SendAsync(HttpMethod.Get, url + "&page=2", bearer: writer.Token))
            .Content.ReadFromJsonAsync<ManagedSkillPage>();
        Assert.Single(next!.Items);
        Assert.Equal(3, page.Items.Concat(next.Items).Select(item => item.Id).Distinct().Count());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.SendAsync(HttpMethod.Get, url.Replace("pageSize=2", "pageSize=101"), bearer: administrator.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await german.SendAsync(HttpMethod.Get, $"/api/v1/manage/skills/{first.Id}", bearer: foreign.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await german.SendAsync(HttpMethod.Get, url, bearer: foreign.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await author.SendAsync(HttpMethod.Post, "/api/v1/manage/skills", new SkillInput(Spanish, "Not allowed", "Grammar"), writer.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client.GetAsync($"/api/v1/manage/skills/{second.Id}")).StatusCode);
        Assert.NotEqual(second.Id, third.Id);
    }

    [Fact]
    public async Task SkillCreationAndEditsRejectDuplicatesAndStaleConcurrentRevisions()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        var input = new SkillInput(Spanish, "Concurrent-" + Guid.NewGuid().ToString("N"), "Grammar");
        var responses = await Task.WhenAll(
            admin.SendAsync(HttpMethod.Post, "/api/v1/manage/skills", input, administrator.Token),
            admin.SendAsync(HttpMethod.Post, "/api/v1/manage/skills", input, administrator.Token));
        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Conflict);
        var skill = (await responses.Single(item => item.StatusCode == HttpStatusCode.Created)
            .Content.ReadFromJsonAsync<ManagedSkill>())!;
        var edits = await Task.WhenAll(
            admin.SendAsync(HttpMethod.Put, $"/api/v1/manage/skills/{skill.Id}",
                new SkillUpdate(input.Name + " first", "Grammar", 1, "Clarify the skill"), administrator.Token),
            admin.SendAsync(HttpMethod.Put, $"/api/v1/manage/skills/{skill.Id}",
                new SkillUpdate(input.Name + " second", "Grammar", 1, "Clarify the skill"), administrator.Token));
        Assert.Single(edits, item => item.StatusCode == HttpStatusCode.OK);
        Assert.Single(edits, item => item.StatusCode == HttpStatusCode.Conflict);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(2, await database.Skills.Where(item => item.Id == skill.Id).Select(item => item.Revision).SingleAsync());
        Assert.Equal(1, await database.AuditLogs.CountAsync(item => item.EntityType == "Skill"
            && item.EntityId == skill.Id.ToString() && item.Action == "SkillEdited"));
    }

    [Fact]
    public async Task ArchivedSkillsPreserveProgressAndExistingTagsButCannotBeNewlyAttached()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        var skill = await CreateSkillAsync(admin, administrator.Token, "Archive-" + Guid.NewGuid().ToString("N"));
        var lesson = await DraftAsync(admin, administrator.Token, await UnitAsync(admin, administrator.Token));
        var input = new ExerciseCreateRequestForTest(lesson.Version, "Translation", "Say hello", "Use Hola", 2, [], ["Hola"], [skill.Id]);
        var created = await admin.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{lesson.Id}/exercises", input, administrator.Token);
        created.EnsureSuccessStatusCode();
        int exerciseCount;
        using (var scope = fixture.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            database.SkillProgress.Add(new SkillProgress { UserId = administrator.Account.Id, SkillId = skill.Id,
                CorrectCount = 1, LastPracticedAt = DateTimeOffset.UtcNow });
            await database.SaveChangesAsync();
            exerciseCount = await database.ExerciseSkills.CountAsync(item => item.SkillId == skill.Id);
        }
        var archived = await admin.SendAsync(HttpMethod.Delete, $"/api/v1/manage/skills/{skill.Id}",
            new SkillArchiveRequest(skill.Revision, "Keep historical progress"), administrator.Token);
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        var detail = await (await admin.SendAsync(HttpMethod.Get, $"/api/v1/manage/skills/{skill.Id}", bearer: administrator.Token))
            .Content.ReadFromJsonAsync<ManagedSkill>();
        Assert.True(detail!.IsArchived);
        using (var scope = fixture.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            Assert.Equal(exerciseCount, await database.ExerciseSkills.CountAsync(item => item.SkillId == skill.Id));
            Assert.Equal(1, await database.SkillProgress.Where(item => item.UserId == administrator.Account.Id && item.SkillId == skill.Id)
                .Select(item => item.CorrectCount).SingleAsync());
        }
        var later = input with { OrderNumber = 3 };
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Post,
            $"/api/v1/manage/lessons/{lesson.Id}/exercises", later, administrator.Token)).StatusCode);
        var response = await created.Content.ReadFromJsonAsync<SkillExerciseResponse>();
        Assert.Equal(HttpStatusCode.OK, (await admin.SendAsync(HttpMethod.Put,
            $"/api/v1/manage/lessons/{lesson.Id}/exercises/{response!.Id}", input, administrator.Token)).StatusCode);
        var restored = await admin.SendAsync(HttpMethod.Put, $"/api/v1/manage/skills/{skill.Id}/visibility",
            new SkillVisibility(false, detail.Revision, "Ready for new exercises"), administrator.Token);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.False((await restored.Content.ReadFromJsonAsync<ManagedSkill>())!.IsArchived);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(HttpMethod.Put,
            $"/api/v1/manage/skills/{skill.Id}/visibility", new { archived = true, revision = 3, reason = "" }, administrator.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Put,
            $"/api/v1/manage/skills/{skill.Id}/visibility", new { archived = true, revision = 3, reason = " a " }, administrator.Token)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentSkillCreationCannotExceedTheCourseCatalogLimit()
    {
        using var first = Browser();
        using var second = Browser();
        var a = await StaffAsync(first, RoleNames.Admin);
        var b = await StaffAsync(second, RoleNames.Admin);
        string prefix = Guid.NewGuid().ToString("N");
        using (var scope = fixture.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            int existing = await database.Skills.CountAsync(item => item.CourseId == German);
            database.Skills.AddRange(Enumerable.Range(existing, 499 - existing).Select(index => new Skill
            {
                CourseId = German, Name = prefix + index, Category = "Topic"
            }));
            await database.SaveChangesAsync();
        }
        var responses = await Task.WhenAll(
            first.SendAsync(HttpMethod.Post, "/api/v1/manage/skills", new SkillInput(German, prefix + " first", "Topic"), a.Token),
            second.SendAsync(HttpMethod.Post, "/api/v1/manage/skills", new SkillInput(German, prefix + " second", "Topic"), b.Token));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        using var checkScope = fixture.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(500, await check.Skills.CountAsync(item => item.CourseId == German));
        var created = (await responses.Single(response => response.StatusCode == HttpStatusCode.Created)
            .Content.ReadFromJsonAsync<ManagedSkill>())!;
        (await first.SendAsync(HttpMethod.Delete, $"/api/v1/manage/skills/{created.Id}",
            new SkillArchiveRequest(created.Revision, "Preserve the history"), a.Token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await first.SendAsync(HttpMethod.Post, "/api/v1/manage/skills",
            new SkillInput(German, prefix + " third", "Topic"), a.Token)).StatusCode);
        var rollback = await Assert.ThrowsAsync<PostgresException>(() => check.Database
            .MigrateAsync("20261120083352_AllowDepartingStaffNoteErasure"));
        Assert.Contains("Retain skill archive and revision history", rollback.MessageText);
        Assert.Contains("20261120085844_AddSkillManagement", await check.Database.GetAppliedMigrationsAsync());
    }

    private async Task<ManagedSkill> CreateSkillAsync(LanguageLearning.Tests.Accounts.SessionTestBrowser browser, string token, string name)
    {
        using var response = await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/skills", new SkillInput(Spanish, name, "Vocabulary"), token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var skill = (await response.Content.ReadFromJsonAsync<ManagedSkill>())!;
        Assert.EndsWith($"/api/v1/manage/skills/{skill.Id}", response.Headers.Location!.ToString());
        using var detail = await browser.SendAsync(HttpMethod.Get, response.Headers.Location.ToString(), bearer: token);
        Assert.Equal(skill, await detail.Content.ReadFromJsonAsync<ManagedSkill>());
        return skill;
    }

    private sealed record ExerciseCreateRequestForTest(Guid VersionId, string Type, string Prompt, string Explanation,
        int OrderNumber, string[] Options, string[] Answers, int[] SkillIds);
    private sealed record SkillExerciseResponse(Guid Id);
}
