using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Tests.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Accounts;

public sealed class AdministrationTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid roleId = await database.Roles.Where(role => role.Name == RoleNames.SuperAdmin).Select(role => role.Id).SingleAsync();
        await database.UserRoles.Where(link => link.RoleId == roleId).ExecuteDeleteAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StudySettingsAreAuditedAndApplyToNewAccountsAndLessons()
    {
        var actor = await AccountAsync(RoleNames.SuperAdmin);
        var administrator = await AccountAsync(RoleNames.Admin);
        using var browser = Browser();
        using var adminBrowser = Browser();
        string access = await actor.LoginAsync(browser);
        string adminAccess = await administrator.LoginAsync(adminBrowser);
        const string path = "/api/v1/admin/settings";
        (await adminBrowser.SendAsync(HttpMethod.Get, path, bearer: adminAccess)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var original = await (await browser.SendAsync(HttpMethod.Get, path, bearer: access))
            .Content.ReadFromJsonAsync<StudySettingsView>();
        var defaults = new StudyDefaults(35, 45, 2, 64, TimeOnly.MinValue, DialogueXp: 17);
        var update = new UpdateStudySettings(defaults, original!.UpdatedAt, "Test the new study defaults.");
        (await adminBrowser.SendAsync(HttpMethod.Put, path, update, adminAccess)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await browser.SendAsync(HttpMethod.Put, path, update, access)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var updated = await (await browser.SendAsync(HttpMethod.Get, path, bearer: access))
            .Content.ReadFromJsonAsync<StudySettingsView>();
        updated!.Defaults.DialogueXp.Should().Be(17);
        (await browser.SendAsync(HttpMethod.Put, path, update with { Defaults = defaults with { LessonXp = 40 } }, access))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await browser.SendAsync(HttpMethod.Put, path, update, access)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var learner = await AccountAsync();
        using var learnerBrowser = Browser();
        string learnerAccess = await learner.LoginAsync(learnerBrowser);
        var lesson = await LearningTestContent.CreateAsync(fixture, learner.Id, count: 1);
        (await learnerBrowser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { lesson.CourseId }, learnerAccess))
            .EnsureSuccessStatusCode();
        (await learnerBrowser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.LessonId}/sessions", bearer: learnerAccess))
            .EnsureSuccessStatusCode();
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var preference = await database.UserPreferences.SingleAsync(item => item.UserId == learner.Id);
        preference.DailyGoalXp.Should().Be(45);
        preference.StudyDays.Should().Be(64);
        preference.ReminderTime.Should().Be(TimeOnly.MinValue);
        (await database.LearningSessions.SingleAsync(item => item.UserId == learner.Id)).FirstCompletionXp.Should().Be(35);
        (await database.AuditLogs.CountAsync(item => item.ActorId == actor.Id && item.Action == "SystemSettingsChanged"))
            .Should().Be(1);
    }

    [Fact]
    public async Task PaginationAndScopeConstraintsAreEnforcedInPostgreSql()
    {
        var actor = await AccountAsync(RoleNames.SuperAdmin);
        using var browser = Browser(); string access = await actor.LoginAsync(browser);
        string prefix = "paged-" + Guid.NewGuid().ToString("N");
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            database.Users.AddRange(Enumerable.Range(0, 21).Select(index => new ApplicationUser
            {
                Id = Guid.NewGuid(),
                DisplayName = $"Page {index:D2}",
                Email = $"{prefix}-{index}@example.test",
                NormalizedEmail = $"{prefix}-{index}@example.test".ToUpperInvariant()
            }));
            int language = await database.Languages.Where(item => item.Code == "es").Select(item => item.Id).SingleAsync();
            database.UserLanguageScopes.Add(new UserLanguageScope { UserId = actor.Id, LanguageId = language });
            await database.SaveChangesAsync();
            await FluentActions.Invoking(() => database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"UserLanguageScopes\" (\"UserId\", \"LanguageId\") VALUES ({actor.Id}, {language})"))
                .Should().ThrowAsync<PostgresException>().Where(exception => exception.SqlState == PostgresErrorCodes.UniqueViolation);
            await FluentActions.Invoking(() => database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"UserLanguageScopes\" (\"UserId\", \"LanguageId\") VALUES ({actor.Id}, {999})"))
                .Should().ThrowAsync<PostgresException>().Where(exception => exception.SqlState == PostgresErrorCodes.ForeignKeyViolation);
        }
        var first = await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/users?search=" + prefix, bearer: access);
        var second = await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/users?search=" + prefix + "&page=2", bearer: access);
        using var a = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var b = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        a.RootElement.GetProperty("total").GetInt32().Should().Be(21);
        a.RootElement.GetProperty("items").GetArrayLength().Should().Be(20);
        b.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        a.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString())
            .Should().NotContain(b.RootElement.GetProperty("items")[0].GetProperty("id").GetString());
    }
    private SessionTestBrowser Browser() => new(fixture.CreateIsolatedClient());
    private async Task<SecurityTestAccount> AccountAsync(string role = RoleNames.Learner)
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        if (role != RoleNames.Learner)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await manager.AddToRoleAsync((await manager.FindByIdAsync(account.Id.ToString()))!, role)).Succeeded.Should().BeTrue();
        }
        return account;
    }
    private static object Roles(string[] roles, int[]? languages = null, string password = SecurityTestAccount.Password) =>
        new { roles, languageIds = languages ?? [], reason = "Approved teaching team access", currentPassword = password };
    private static object Status(string status) => new { status, reason = "Account access review decision", currentPassword = SecurityTestAccount.Password };

    [Theory]
    [InlineData(RoleNames.Learner)]
    [InlineData(RoleNames.Teacher)]
    [InlineData(RoleNames.ContentAuthor)]
    [InlineData(RoleNames.Reviewer)]
    [InlineData(RoleNames.Moderator)]
    public async Task OtherRolesCannotReadOrMutateAdministration(string role)
    {
        var account = await AccountAsync(role); using var browser = Browser(); string access = await account.LoginAsync(browser);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/users", bearer: access)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/audit-logs", bearer: access)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{account.Id}/roles", Roles([RoleNames.Learner, RoleNames.SuperAdmin]), access)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/admin/users/{account.Id}/status", Status("Blocked"), access)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoleAndLanguageChangesPersistAuditAndRevokeTargetSessions()
    {
        var actor = await AccountAsync(RoleNames.SuperAdmin); using var browser = Browser(); string access = await actor.LoginAsync(browser);
        var target = await AccountAsync(); using var targetBrowser = Browser(); string targetAccess = await target.LoginAsync(targetBrowser);
        int spanish;
        await using (var scope = fixture.Services.CreateAsyncScope()) spanish = await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().Languages.Where(item => item.Code == "es").Select(item => item.Id).SingleAsync();
        (await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{target.Id}/roles", Roles([RoleNames.Learner, RoleNames.Reviewer], [spanish]), access)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await targetBrowser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: targetAccess)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var check = fixture.Services.CreateAsyncScope(); var database = check.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.UserLanguageScopes.SingleAsync(item => item.UserId == target.Id)).LanguageId.Should().Be(spanish);
        var audit = await database.AuditLogs.SingleAsync(item => item.EntityId == target.Id.ToString() && item.Action == "UserRolesChanged");
        audit.ActorId.Should().Be(actor.Id); audit.Reason.Should().Contain(RoleNames.Reviewer); audit.TraceId.Should().NotBeEmpty();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await manager.IsInRoleAsync((await manager.FindByIdAsync(target.Id.ToString()))!, RoleNames.Reviewer)).Should().BeTrue();
    }

    [Fact]
    public async Task AdminCanBlockLearnersButCannotEditRolesOrBlockAdministrators()
    {
        var actor = await AccountAsync(RoleNames.Admin); using var browser = Browser(); string access = await actor.LoginAsync(browser);
        var target = await AccountAsync(); using var second = Browser(); string targetAccess = await target.LoginAsync(second);
        (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/admin/users/{target.Id}/status", Status("Blocked"), access)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await second.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: targetAccess)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/admin/users/{target.Id}/status", Status("Active"), access)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{target.Id}/roles", Roles([RoleNames.Learner, RoleNames.Admin]), access)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var administrator = await AccountAsync(RoleNames.SuperAdmin);
        (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/admin/users/{administrator.Id}/status", Status("Blocked"), access)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LastActiveSuperAdminCannotBeBlockedOrDemotedIncludingRaces()
    {
        var first = await AccountAsync(RoleNames.SuperAdmin); using var browser = Browser(); string firstAccess = await first.LoginAsync(browser);
        (await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{first.Id}/roles", Roles([RoleNames.Learner]), firstAccess)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/admin/users/{first.Id}/status", Status("Blocked"), firstAccess)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var second = await AccountAsync(RoleNames.SuperAdmin); using var other = Browser(); string secondAccess = await second.LoginAsync(other);
        var responses = await Task.WhenAll(browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{first.Id}/roles", Roles([RoleNames.Learner]), firstAccess), other.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{second.Id}/roles", Roles([RoleNames.Learner]), secondAccess));
        responses.Count(item => item.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        responses.Count(item => item.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
    }

    [Fact]
    public async Task EnrollmentUnknownRolesLanguagesAndWrongProofAreRejected()
    {
        var actor = await AccountAsync(RoleNames.SuperAdmin); using var browser = Browser(); string access = await actor.LoginAsync(browser);
        var target = await AccountAsync();
        (await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{target.Id}/roles", Roles([RoleNames.Learner, "Owner"]), access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{target.Id}/roles", Roles([RoleNames.Learner, RoleNames.Admin], [999]), access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{target.Id}/roles", Roles([RoleNames.Learner, RoleNames.Admin], password: "Wrong password 42!"), access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using (var scope = fixture.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().Users.Where(item => item.Id == target.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.TwoFactorEnabled, false));
        (await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{target.Id}/roles", Roles([RoleNames.Learner, RoleNames.Admin]), access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/admin/users/{target.Id}/status", new { status = "Blocked", reason = "", currentPassword = SecurityTestAccount.Password }, access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchPaginationAuditVisibilityAndOldSignInAreBounded()
    {
        var actor = await AccountAsync(RoleNames.SuperAdmin); using var browser = Browser(); string access = await actor.LoginAsync(browser);
        var target = await AccountAsync();
        await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{target.Id}/roles", Roles([RoleNames.Learner, RoleNames.Teacher]), access);
        await browser.SendAsync(HttpMethod.Patch, $"/api/v1/admin/users/{target.Id}/status", Status("Blocked"), access);
        var admin = await AccountAsync(RoleNames.Admin); using var other = Browser(); string adminAccess = await admin.LoginAsync(other);
        var roster = await other.SendAsync(HttpMethod.Get, "/api/v1/admin/users?search=" + Uri.EscapeDataString(target.Email) + "&status=Blocked&role=Teacher", bearer: adminAccess);
        roster.EnsureSuccessStatusCode(); using var data = JsonDocument.Parse(await roster.Content.ReadAsStringAsync());
        data.RootElement.GetProperty("total").GetInt32().Should().Be(1);
        (await roster.Content.ReadAsStringAsync()).Should().NotContain("passwordHash").And.NotContain("securityStamp");
        var history = await other.SendAsync(HttpMethod.Get, "/api/v1/admin/audit-logs?userId=" + target.Id, bearer: adminAccess);
        using var records = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        records.RootElement.GetProperty("items").EnumerateArray().Should().OnlyContain(item => item.GetProperty("action").GetString() == "UserStatusChanged");
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/users?page=0", bearer: access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await actor.ExpireFamilyAuthenticationAsync(fixture);
        (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/admin/users/{target.Id}/status", Status("Active"), access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fixture.Client.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
