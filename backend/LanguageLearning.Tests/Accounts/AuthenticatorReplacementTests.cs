using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class AuthenticatorReplacementTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private SessionTestBrowser Browser() => new(fixture.CreateIsolatedClient());
    private static Task<HttpResponseMessage> BeginAsync(SessionTestBrowser browser, string access, string password = SecurityTestAccount.Password) =>
        browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/replacement", new { currentPassword = password }, access);

    private static Task<HttpResponseMessage> ConfirmAsync(SessionTestBrowser browser, string access, ReplacementSetup setup, string? code = null) =>
        browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/replacement/confirm",
            new { requestId = setup.RequestId, currentPassword = SecurityTestAccount.Password, code = code ?? TestAuthenticator.Code(setup.SharedKey) }, access);

    [Fact]
    public async Task LastRecoveryCodeCanReplaceAuthenticatorWithoutDisablingMfa()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture, 1);
        using var browser = Browser();
        string access = await account.LoginAsync(browser);
        var setup = (await (await BeginAsync(browser, access)).Content.ReadFromJsonAsync<ReplacementSetup>())!;
        var retry = await (await BeginAsync(browser, access)).Content.ReadFromJsonAsync<ReplacementSetup>();
        retry.Should().Be(setup);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(account.Id.ToString()))!;
            user.TwoFactorEnabled.Should().BeTrue();
            (await users.GetAuthenticatorKeyAsync(user)).Should().Be(account.Key);
            (await users.CountRecoveryCodesAsync(user)).Should().Be(0);
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var stored = await database.UserTokens.SingleAsync(item => item.UserId == account.Id && item.Name == "PendingAuthenticatorReplacement");
            stored.Value.Should().NotContain(setup.SharedKey);
        }
        var response = await ConfirmAsync(browser, access, setup);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        result.RootElement.GetProperty("recoveryCodes").GetArrayLength().Should().Be(10);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: access)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var manager = finalScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var changed = (await manager.FindByIdAsync(account.Id.ToString()))!;
        changed.TwoFactorEnabled.Should().BeTrue();
        (await manager.GetAuthenticatorKeyAsync(changed)).Should().Be(setup.SharedKey);
        (await manager.GetAuthenticatorKeyAsync(changed)).Should().NotBe(account.Key);
        (await manager.CountRecoveryCodesAsync(changed)).Should().Be(10);
        var finalDatabase = finalScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await finalDatabase.AuditLogs.SingleAsync(item => item.EntityId == account.Id.ToString())).Action.Should().Be("AuthenticatorReplaced");
    }

    [Fact]
    public async Task WrongNewCodeKeepsOldKeyAndCountsTowardLockout()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture); using var browser = Browser();
        string access = await account.LoginAsync(browser);
        var setup = (await (await BeginAsync(browser, access)).Content.ReadFromJsonAsync<ReplacementSetup>())!;
        var validWindow = Enumerable.Range(-3, 7).Select(offset => TestAuthenticator.Code(setup.SharedKey, offset)).ToHashSet();
        string invalid = Enumerable.Range(0, 100).Select(value => value.ToString("D6")).First(value => !validWindow.Contains(value));
        (await ConfirmAsync(browser, access, setup, invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(account.Id.ToString()))!;
        (await users.GetAuthenticatorKeyAsync(user)).Should().Be(account.Key);
        user.AccessFailedCount.Should().Be(1);
        (await users.CountRecoveryCodesAsync(user)).Should().Be(9);
    }

    [Fact]
    public async Task RotationCannotMakeAnOldMfaSignInRecentAgain()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture); using var browser = Browser();
        _ = await account.LoginAsync(browser);
        await account.ExpireFamilyAuthenticationAsync(fixture);
        var rotated = await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", new { }, csrf: true);
        rotated.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await rotated.Content.ReadAsStringAsync());
        string access = body.RootElement.GetProperty("accessToken").GetString()!;
        (await BeginAsync(browser, access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PendingSetupCannotBeRedeemedFromAnotherSessionOrAccount()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture); using var browser = Browser();
        string access = await account.LoginAsync(browser);
        var setup = (await (await BeginAsync(browser, access)).Content.ReadFromJsonAsync<ReplacementSetup>())!;
        using var second = Browser(); string otherSession = await account.LoginAsync(second, 1);
        (await ConfirmAsync(second, otherSession, setup)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var other = await SecurityTestAccount.CreateAsync(fixture); using var third = Browser();
        string otherAccess = await other.LoginAsync(third);
        (await ConfirmAsync(third, otherAccess, setup)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ConfirmAsync(browser, access, setup)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExpiredPendingSetupDoesNotChangeActiveKey()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture); using var browser = Browser();
        string access = await account.LoginAsync(browser);
        var setup = (await (await BeginAsync(browser, access)).Content.ReadFromJsonAsync<ReplacementSetup>())!;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(account.Id.ToString()))!;
            var pending = JsonNode.Parse((await users.GetAuthenticationTokenAsync(user, "LexiGo", "PendingAuthenticatorReplacement"))!)!;
            pending["ExpiresAt"] = DateTimeOffset.UtcNow.AddSeconds(-1);
            await users.SetAuthenticationTokenAsync(user, "LexiGo", "PendingAuthenticatorReplacement", pending.ToJsonString());
        }
        (await ConfirmAsync(browser, access, setup)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConcurrentConfirmationsConsumePendingOnce()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture); using var browser = Browser();
        string access = await account.LoginAsync(browser);
        var setup = (await (await BeginAsync(browser, access)).Content.ReadFromJsonAsync<ReplacementSetup>())!;
        using var second = Browser();
        var responses = await Task.WhenAll(ConfirmAsync(browser, access, setup), ConfirmAsync(second, access, setup));
        responses.Count(item => item.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Should().OnlyContain(item => item.StatusCode == HttpStatusCode.OK || item.StatusCode == HttpStatusCode.BadRequest || item.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PrivilegedReplacementPreservesRolesAndCannotBeUsedToDisableMfa()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await users.AddToRoleAsync((await users.FindByIdAsync(account.Id.ToString()))!, RoleNames.Admin);
        }
        using var browser = Browser(); string access = await account.LoginAsync(browser);
        var setup = (await (await BeginAsync(browser, access)).Content.ReadFromJsonAsync<ReplacementSetup>())!;
        (await ConfirmAsync(browser, access, setup)).StatusCode.Should().Be(HttpStatusCode.OK);
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var manager = finalScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var changed = (await manager.FindByIdAsync(account.Id.ToString()))!;
        changed.TwoFactorEnabled.Should().BeTrue();
        (await manager.IsInRoleAsync(changed, RoleNames.Admin)).Should().BeTrue();
        (await manager.RedeemTwoFactorRecoveryCodeAsync(changed, account.RecoveryCodes[1])).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task AnonymousWrongPasswordAndOverpostingAreDenied()
    {
        using var browser = Browser();
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/replacement", new { currentPassword = SecurityTestAccount.Password })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var account = await SecurityTestAccount.CreateAsync(fixture); string access = await account.LoginAsync(browser);
        (await BeginAsync(browser, access, "Wrong password 42!")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/replacement", new { currentPassword = SecurityTestAccount.Password, userId = Guid.NewGuid() }, access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
