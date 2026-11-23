using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace LanguageLearning.Tests.Accounts;

public sealed class TwoFactorTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string Password = "Authenticator test passphrase 42!";
    private SessionTestBrowser Browser() => new(fixture.CreateIsolatedClient());

    private async Task<(Guid Id, string Email, string Access)> LearnerAsync(SessionTestBrowser browser)
    {
        string email = $"mfa-{Guid.NewGuid():N}@example.test";
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = "MFA Learner", password = Password });
        var link = await fixture.ConfirmationAsync(email);
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = link.UserId, token = link.Token });
        return (link.UserId, email, await AccessAsync(await LoginAsync(browser, email)));
    }

    private static Task<HttpResponseMessage> LoginAsync(SessionTestBrowser browser, string email, string? code = null, string? recoveryCode = null) =>
        browser.SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password = Password, authenticatorCode = code, recoveryCode }, csrf: true);

    private static async Task<string> AccessAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<(string Key, string[] Codes, string SetupCode)> EnrollAsync(SessionTestBrowser browser, string access)
    {
        var response = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/setup", new { currentPassword = Password }, access);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var setup = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string key = setup.RootElement.GetProperty("sharedKey").GetString()!;
        string setupCode = TestAuthenticator.Code(key, -1);
        var enabled = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/enable", new { currentPassword = Password, code = setupCode }, access);
        enabled.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await enabled.Content.ReadAsStringAsync());
        return (key, body.RootElement.GetProperty("recoveryCodes").EnumerateArray().Select(code => code.GetString()!).ToArray(), setupCode);
    }

    [Fact]
    public async Task EnrollmentEncryptsSecretsAndRequiresSecondFactorForLogin()
    {
        using var browser = Browser();
        var learner = await LearnerAsync(browser);
        var enrollment = await EnrollAsync(browser, learner.Access);
        enrollment.Codes.Should().HaveCount(10).And.OnlyHaveUniqueItems();
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: learner.Access)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var records = await database.UserTokens.Where(token => token.UserId == learner.Id).Select(token => token.Value).ToArrayAsync();
            records.Should().NotContain(enrollment.Key);
            records.Should().OnlyContain(value => value != null && !enrollment.Codes.Any(code => value.Contains(code)));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.GetAuthenticatorKeyAsync((await users.FindByIdAsync(learner.Id.ToString()))!)).Should().Be(enrollment.Key);
        }
        var challenge = await LoginAsync(browser, learner.Email);
        challenge.StatusCode.Should().Be(HttpStatusCode.OK);
        (await challenge.Content.ReadAsStringAsync()).Should().Contain("requiresTwoFactor").And.NotContain("accessToken");
        (await LoginAsync(browser, learner.Email, "invalid")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        string code = TestAuthenticator.Code(enrollment.Key);
        string access = await AccessAsync(await LoginAsync(browser, learner.Email, code));
        new JsonWebToken(access).Claims.Should().Contain(claim => claim.Type == "amr" && claim.Value == "mfa");
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: access)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(browser, learner.Email, enrollment.SetupCode)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(browser, learner.Email, code)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConcurrentRecoveryCodeRedemptionOnlySucceedsOnce()
    {
        using var browser = Browser();
        var learner = await LearnerAsync(browser);
        var enrollment = await EnrollAsync(browser, learner.Access);
        using var other = Browser();
        var results = await Task.WhenAll(LoginAsync(browser, learner.Email, recoveryCode: enrollment.Codes[0]), LoginAsync(other, learner.Email, recoveryCode: enrollment.Codes[0]));
        results.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Count(response => response.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);
    }

    [Fact]
    public async Task PrivilegedAccountCanSignInWithMfaButCannotDisableIt()
    {
        using var browser = Browser();
        var learner = await LearnerAsync(browser);
        var enrollment = await EnrollAsync(browser, learner.Access);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.AddToRoleAsync((await users.FindByIdAsync(learner.Id.ToString()))!, RoleNames.Admin)).Succeeded.Should().BeTrue();
        }
        string access = await AccessAsync(await LoginAsync(browser, learner.Email, recoveryCode: enrollment.Codes[0]));
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/disable", new { currentPassword = Password, recoveryCode = enrollment.Codes[1] }, access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var status = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/two-factor", bearer: access);
        (await status.Content.ReadAsStringAsync()).Should().Contain("\"enabled\":true");
    }

    [Fact]
    public async Task ReplacingRecoveryCodesRevokesSessionsAndOldCodes()
    {
        using var browser = Browser();
        var learner = await LearnerAsync(browser);
        var enrollment = await EnrollAsync(browser, learner.Access);
        string access = await AccessAsync(await LoginAsync(browser, learner.Email, recoveryCode: enrollment.Codes[0]));
        var response = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/recovery-codes", new { currentPassword = Password, code = TestAuthenticator.Code(enrollment.Key) }, access);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string replacement = body.RootElement.GetProperty("recoveryCodes")[0].GetString()!;
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: access)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(browser, learner.Email, recoveryCode: enrollment.Codes[1])).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(browser, learner.Email, recoveryCode: replacement)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LearnerCanRecoverAndDisableMfaWithoutLeavingOldSessionsActive()
    {
        using var browser = Browser();
        var learner = await LearnerAsync(browser);
        var enrollment = await EnrollAsync(browser, learner.Access);
        string access = await AccessAsync(await LoginAsync(browser, learner.Email, recoveryCode: enrollment.Codes[0]));
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/disable", new { currentPassword = Password, recoveryCode = enrollment.Codes[1] }, access)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: access)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        string nextAccess = await AccessAsync(await LoginAsync(browser, learner.Email));
        var status = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/two-factor", bearer: nextAccess);
        (await status.Content.ReadAsStringAsync()).Should().Contain("\"enabled\":false").And.Contain("\"recoveryCodesRemaining\":0");
    }

    [Fact]
    public async Task RepeatedIncorrectSettingsPasswordsLockSignIn()
    {
        using var browser = Browser();
        var learner = await LearnerAsync(browser);
        for (int index = 0; index < 5; index++)
            (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/setup", new { currentPassword = "Incorrect current password!" }, learner.Access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LoginAsync(browser, learner.Email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PasswordResetCannotRemoveTheSecondFactor()
    {
        using var browser = Browser();
        var learner = await LearnerAsync(browser);
        await EnrollAsync(browser, learner.Access);
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = learner.Email });
        string token;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var message = await database.EmailOutboxMessages.SingleAsync(message => message.UserId == learner.Id && message.Purpose == "PasswordReset");
            token = System.Text.RegularExpressions.Regex.Match(message.HtmlBody, "token=([A-Za-z0-9_-]+)").Groups[1].Value;
        }
        const string replacement = "New MFA recovery password 42!";
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId = learner.Id, token, newPassword = replacement })).StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email = learner.Email, password = replacement }, csrf: true);
        (await response.Content.ReadAsStringAsync()).Should().Contain("requiresTwoFactor").And.NotContain("accessToken");
    }

    [Fact]
    public async Task SettingsRequireOwnershipPasswordAndValidCode()
    {
        using var browser = Browser();
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/setup", new { currentPassword = Password })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var learner = await LearnerAsync(browser);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/setup", new { currentPassword = "Incorrect long password!" }, learner.Access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/enable", new { currentPassword = Password, code = "invalid" }, learner.Access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/two-factor/setup", new { currentPassword = Password, userId = Guid.NewGuid() }, learner.Access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

}
