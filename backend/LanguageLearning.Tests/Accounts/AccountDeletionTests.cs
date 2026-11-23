using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class AccountDeletionTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task ResponsibilityChecklistIsOwnedAndClosingTeachingWorkUnblocksDeletion()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var otherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await SecurityTestAccount.CreateAsync(fixture);
        var other = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(teacher.Id.ToString()))!, RoleNames.Teacher)).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var courseId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var classroom = new Classroom { TeacherId = teacher.Id, CourseId = courseId, Name = "Synthetic handoff class" };
        var offer = new ExtraStudyOffer { TeacherId = teacher.Id, CourseId = courseId, Enabled = true };
        var request = new ExtraStudyRequest { OfferId = offer.Id, LearnerId = other.Id, Status = "Accepted", CreatedAt = DateTimeOffset.UtcNow };
        db.Classrooms.Add(classroom);
        db.ExtraStudyOffers.Add(offer);
        db.ExtraStudyRequests.Add(request);
        await db.SaveChangesAsync();
        string token = await teacher.LoginAsync(browser);
        string otherToken = await other.LoginAsync(otherBrowser);
        const string checklist = "/api/v1/me/deletion-responsibilities";
        var response = await browser.SendAsync(HttpMethod.Get, checklist, bearer: token);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var pending = (await response.Content.ReadFromJsonAsync<DeletionResponsibility[]>())!;
        Assert.Equal(new[] { "classes", "extra-study" }, pending.Select(item => item.Code));
        var otherResponse = await otherBrowser.SendAsync(HttpMethod.Get, checklist + "?userId=" + teacher.Id, bearer: otherToken);
        Assert.Empty((await otherResponse.Content.ReadFromJsonAsync<DeletionResponsibility[]>())!);

        var confirmation = new { currentPassword = SecurityTestAccount.Password, confirmation = "DELETE", recoveryCode = teacher.RecoveryCodes[1] };
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", confirmation, token, csrf: true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/classes/{classroom.Id}",
            new { name = classroom.Name, description = "", isArchived = true, revision = classroom.Revision }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Put,
            $"/api/v1/extra-study/availability/{courseId}", new { enabled = false }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Delete,
            $"/api/v1/extra-study/requests/{request.Id}", bearer: token)).StatusCode);
        var cleared = await browser.SendAsync(HttpMethod.Get, checklist, bearer: token);
        Assert.Empty((await cleared.Content.ReadFromJsonAsync<DeletionResponsibility[]>())!);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", confirmation, token, csrf: true)).StatusCode);
        Assert.Equal("Closed", await db.ExtraStudyRequests.Where(item => item.Id == request.Id).Select(item => item.Status).SingleAsync());
        Assert.True(await db.Classrooms.Where(item => item.Id == classroom.Id).Select(item => item.IsArchived).SingleAsync());
    }

    [Fact]
    public async Task ResponsibilityChecklistRejectsAnonymousAndRevokedSessions()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        const string path = "/api/v1/me/deletion-responsibilities";
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, path)).StatusCode);
        var account = await SecurityTestAccount.CreateAsync(fixture);
        string token = await account.LoginAsync(browser);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.RefreshSessions.Where(item => item.UserId == account.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, path, bearer: token)).StatusCode);
    }

    [Fact]
    public async Task LastAdministratorAndActiveTeacherResponsibilitiesBlockDeletion()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var user = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(user.Id.ToString()))!, RoleNames.SuperAdmin)).Succeeded);
        string token = await user.LoginAsync(browser);
        var verification = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/deletion-verification", bearer: token);
        verification.EnsureSuccessStatusCode();
        Assert.True(verification.Headers.CacheControl?.NoStore);
        Assert.False(string.IsNullOrWhiteSpace(
            (await verification.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestToken").GetString()));
        var deletion = new
        {
            currentPassword = SecurityTestAccount.Password,
            confirmation = "DELETE",
            recoveryCode = user.RecoveryCodes[1]
        };
        var blocked = await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", deletion, token, csrf: true);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("last active Super Admin", await blocked.Content.ReadAsStringAsync());
        var second = await SecurityTestAccount.CreateAsync(fixture);
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(second.Id.ToString()))!, RoleNames.SuperAdmin)).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        db.Classrooms.Add(new Classroom
        {
            TeacherId = user.Id,
            CourseId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Name = "Active class"
        });
        await db.SaveChangesAsync();
        var teaching = await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", deletion, token, csrf: true);
        Assert.Equal(HttpStatusCode.Conflict, teaching.StatusCode);
        Assert.Contains("active classes", await teaching.Content.ReadAsStringAsync());
        Assert.Equal("Active", await db.Users.Where(item => item.Id == user.Id).Select(item => item.Status).SingleAsync());
    }

    [Fact]
    public async Task WrongPasswordOrMissingSecondFactorPreservesAccount()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var user = await SecurityTestAccount.CreateAsync(fixture);
        string token = await user.LoginAsync(browser);
        var wrong = await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = "Wrong password",
            confirmation = "DELETE",
            recoveryCode = user.RecoveryCodes[1]
        }, token, csrf: true);
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        var missing = await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password,
            confirmation = "DELETE"
        }, token, csrf: true);
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/export", bearer: token)).StatusCode);
    }
}
