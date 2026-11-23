using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Tests.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Billing;

public sealed class BillingPermissionTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private async Task<SecurityTestAccount> Account(string role, bool claim = false)
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(account.Id.ToString()))!;
        if (role != RoleNames.Learner) Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        if (claim) Assert.True((await users.AddClaimAsync(user, new Claim(BillingPermissions.ClaimType, BillingPermissions.Manage))).Succeeded);
        return account;
    }
    private static Task<HttpResponseMessage> Set(SessionTestBrowser browser, string token, Guid target, bool enabled,
        Guid? key = null, string password = SecurityTestAccount.Password) => browser.SendAsync(HttpMethod.Put,
        $"/api/v1/admin/users/{target}/billing-permission", new { enabled, reason = "Reviewed test billing delegation", currentPassword = password, idempotencyKey = key ?? Guid.NewGuid() }, token);

    [Theory]
    [InlineData(RoleNames.Learner)]
    [InlineData(RoleNames.Teacher)]
    [InlineData(RoleNames.ContentAuthor)]
    [InlineData(RoleNames.Reviewer)]
    [InlineData(RoleNames.Moderator)]
    [InlineData(RoleNames.Admin)]
    public async Task StaffRolesDoNotImplicitlyReceiveFinancialAccess(string role)
    {
        var account = await Account(role, role != RoleNames.Admin);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); string token = await account.LoginAsync(browser);
        foreach (string path in new[] { "/api/v1/admin/plans", "/api/v1/admin/payments", "/api/v1/admin/subscriptions", "/api/v1/admin/billing-audit" })
            Assert.Equal(HttpStatusCode.Forbidden, (await browser.SendAsync(HttpMethod.Get, path, bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.SendAsync(HttpMethod.Post, $"/api/v1/admin/payments/{Guid.NewGuid()}/refunds", new { amount = 1, reason = "Not authorized", currentPassword = SecurityTestAccount.Password, idempotencyKey = Guid.NewGuid() }, token)).StatusCode);
        var current = await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: token)).Content.ReadFromJsonAsync<AccountSummary>();
        Assert.DoesNotContain(BillingPermissions.Manage, current!.Permissions!);
    }

    [Fact]
    public async Task SuperAdminCanDelegateAndRevokeWithImmediateSessionRotation()
    {
        var actor = await Account(RoleNames.SuperAdmin); var target = await Account(RoleNames.Admin);
        using var superBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient()); string rootToken = await actor.LoginAsync(superBrowser);
        using var adminBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient()); string old = await target.LoginAsync(adminBrowser);
        Guid key = Guid.NewGuid(); var grant = await Set(superBrowser, rootToken, target.Id, true, key); grant.EnsureSuccessStatusCode();
        var result = (await grant.Content.ReadFromJsonAsync<BillingPermissionView>())!; Assert.True(result.Granted); Assert.True(result.Eligible);
        Assert.Equal(HttpStatusCode.Unauthorized, (await adminBrowser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: old)).StatusCode);
        string token = await target.LoginAsync(adminBrowser, 1);
        var current = (await (await adminBrowser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: token)).Content.ReadFromJsonAsync<AccountSummary>())!;
        Assert.Contains(BillingPermissions.Manage, current.Permissions!);
        Assert.Equal(HttpStatusCode.OK, (await adminBrowser.SendAsync(HttpMethod.Get, "/api/v1/admin/payments", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Set(adminBrowser, token, actor.Id, false)).StatusCode);
        (await Set(superBrowser, rootToken, target.Id, false)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await adminBrowser.SendAsync(HttpMethod.Get, "/api/v1/admin/payments", bearer: token)).StatusCode);
        string denied = await target.LoginAsync(adminBrowser, 2);
        Assert.Equal(HttpStatusCode.Forbidden, (await adminBrowser.SendAsync(HttpMethod.Get, "/api/v1/admin/payments", bearer: denied)).StatusCode);
        var replay = await Set(superBrowser, rootToken, target.Id, true, key); replay.EnsureSuccessStatusCode();
        Assert.True((await replay.Content.ReadFromJsonAsync<BillingPermissionView>())!.Granted);
        var actual = (await (await superBrowser.SendAsync(HttpMethod.Get, $"/api/v1/admin/users/{target.Id}/billing-permission", bearer: rootToken)).Content.ReadFromJsonAsync<BillingPermissionView>())!;
        Assert.False(actual.Granted);
        Assert.Equal(HttpStatusCode.Conflict, (await Set(superBrowser, rootToken, target.Id, false, key)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var snapshot = await db.BillingOperations.Where(item => item.UserId == actor.Id && item.IdempotencyKey == key).Select(item => item.RequestSnapshot).SingleAsync();
        Assert.DoesNotContain(SecurityTestAccount.Password, snapshot);
    }

    [Fact]
    public async Task ASessionWithoutVerifiedMfaCannotReadOrMutateBilling()
    {
        var actor = await Account(RoleNames.SuperAdmin);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var token = await actor.LoginAsync(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.RefreshSessions.Where(item => item.UserId == actor.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.MfaVerified, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/payments", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/plans", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Post, $"/api/v1/admin/payments/{Guid.NewGuid()}/refunds", new { amount = 1, reason = "MFA required", currentPassword = SecurityTestAccount.Password, idempotencyKey = Guid.NewGuid() }, token)).StatusCode);
    }

    [Fact]
    public async Task GrantsNeedEligibleAdminFreshMfaAndCurrentPassword()
    {
        var actor = await Account(RoleNames.SuperAdmin); var target = await Account(RoleNames.Admin); var teacher = await Account(RoleNames.Teacher);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var token = await actor.LoginAsync(browser);
        Assert.Equal(HttpStatusCode.Conflict, (await Set(browser, token, teacher.Id, true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Set(browser, token, target.Id, true, password: "Wrong password 42!")).StatusCode);
        await actor.ExpireFamilyAuthenticationAsync(fixture);
        Assert.Equal(HttpStatusCode.Conflict, (await Set(browser, token, target.Id, true)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.False(await db.UserClaims.AnyAsync(item => item.UserId == target.Id && item.ClaimValue == BillingPermissions.Manage));
    }

    [Fact]
    public async Task RemovingAdminAlsoRemovesOldDelegationBeforeARegrantCouldReactivateIt()
    {
        var actor = await Account(RoleNames.SuperAdmin); var target = await Account(RoleNames.Admin, true);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var token = await actor.LoginAsync(browser);
        (await browser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{target.Id}/roles", new { roles = new[] { RoleNames.Learner }, languageIds = Array.Empty<int>(), reason = "Removed administrative duties", currentPassword = SecurityTestAccount.Password }, token)).EnsureSuccessStatusCode();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.False(await db.UserClaims.AnyAsync(item => item.UserId == target.Id && item.ClaimType == BillingPermissions.ClaimType && item.ClaimValue == BillingPermissions.Manage));
    }

    [Fact]
    public async Task FailedReplayEvidenceInsertRollsBackGrantStampAndSessionRevocation()
    {
        var actor = await Account(RoleNames.SuperAdmin); var target = await Account(RoleNames.Admin);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var token = await actor.LoginAsync(browser);
        using var targetBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var original = await target.LoginAsync(targetBrowser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fail_permission_fixture() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Operation\" = 'Billing.Permission' THEN RAISE EXCEPTION 'injected replay failure'; END IF; RETURN NEW; END; $$; CREATE TRIGGER fail_permission_fixture BEFORE INSERT ON \"BillingOperations\" FOR EACH ROW EXECUTE FUNCTION fail_permission_fixture();");
        Guid key = Guid.NewGuid();
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await Set(browser, token, target.Id, true, key)).StatusCode);
            Assert.False(await db.UserClaims.AnyAsync(item => item.UserId == target.Id && item.ClaimValue == BillingPermissions.Manage));
            Assert.Equal(HttpStatusCode.OK, (await targetBrowser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: original)).StatusCode);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_permission_fixture ON \"BillingOperations\"; DROP FUNCTION fail_permission_fixture();"); }
        (await Set(browser, token, target.Id, true, key)).EnsureSuccessStatusCode();
    }
}
