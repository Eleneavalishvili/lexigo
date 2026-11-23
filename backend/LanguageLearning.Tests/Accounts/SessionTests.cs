using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LanguageLearning.Api.Authentication;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LanguageLearning.Tests.Accounts;

public sealed class SessionTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string Password = "The test learner passphrase 42!";
    private static string Email() => $"session-{Guid.NewGuid():N}@example.test";
    private SessionTestBrowser Browser() => new(fixture.CreateIsolatedClient());

    private async Task<(Guid Id, string Email)> LearnerAsync(bool confirm = true)
    {
        string email = Email();
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = "Test Learner", password = Password })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var link = await fixture.ConfirmationAsync(email);
        if (confirm) (await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = link.UserId, token = link.Token })).StatusCode.Should().Be(HttpStatusCode.OK);
        return (link.UserId, email);
    }

    private static Task<HttpResponseMessage> LoginAsync(SessionTestBrowser browser, string email, string password = Password, bool csrf = true) =>
        browser.SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password }, csrf: csrf);

    private static async Task<string> AccessAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task LoginRequiresConfirmedEmailAndCsrf()
    {
        var learner = await LearnerAsync(false);
        using var browser = Browser();
        (await LoginAsync(browser, learner.Email, csrf: false)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LoginAsync(browser, learner.Email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConfirmedLoginSetsHttpOnlyStrictCookieAndOnlyStoresItsHash()
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        var response = await LoginAsync(browser, learner.Email);
        string access = await AccessAsync(response);
        response.Headers.GetValues("Set-Cookie").Should().Contain(header => header.StartsWith("lexigo.refresh=") && header.Contains("httponly") && header.Contains("samesite=strict"));
        (await response.Content.ReadAsStringAsync()).Should().NotContain("refreshToken");
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var session = await database.RefreshSessions.SingleAsync(item => item.UserId == learner.Id);
        session.TokenHash.Should().NotBe(browser.RefreshToken);
        session.TokenHash.Should().HaveLength(64);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: access)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WrongAndUnknownCredentialsHaveSamePublicError()
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        var wrong = await LoginAsync(browser, learner.Email, "This is the wrong password 999");
        var unknown = await LoginAsync(browser, Email(), "This is the wrong password 999");
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(wrong.StatusCode);
        using var a = JsonDocument.Parse(await wrong.Content.ReadAsStringAsync());
        using var b = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync());
        a.RootElement.GetProperty("title").GetString().Should().Be(b.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task FailedAttemptsLockTheAccount()
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        for (int index = 0; index < 5; index++) await LoginAsync(browser, learner.Email, "The wrong long password 123!");
        (await LoginAsync(browser, learner.Email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var scope = fixture.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().IsLockedOutAsync((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(learner.Id.ToString()))!)).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshRotatesAndOldTokenReuseRevokesFamily()
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        string firstAccess = await AccessAsync(await LoginAsync(browser, learner.Email));
        string originalRefresh = browser.RefreshToken!;
        string nextAccess = await AccessAsync(await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", csrf: true));
        browser.RefreshToken.Should().NotBe(originalRefresh);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: firstAccess)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var replay = Browser();
        replay.ReplaceRefresh(originalRefresh);
        (await replay.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", csrf: true)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: nextAccess)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SimultaneousRefreshCannotCreateTwoLiveSuccessors()
    {
        var learner = await LearnerAsync();
        using var original = Browser();
        await LoginAsync(original, learner.Email);
        using var rival = Browser();
        rival.ReplaceRefresh(original.RefreshToken!);
        var results = await Task.WhenAll(
            original.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", csrf: true),
            rival.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", csrf: true));
        results.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Count(response => response.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.RefreshSessions.CountAsync(item => item.UserId == learner.Id && item.RevokedAt == null)).Should().Be(0);
    }

    [Fact]
    public async Task RefreshRequiresCsrfAndCookie()
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        await LoginAsync(browser, learner.Email);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var empty = Browser();
        (await empty.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", csrf: true)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutInvalidatesExistingAccessImmediately()
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        string access = await AccessAsync(await LoginAsync(browser, learner.Email));
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/logout", csrf: true)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: access)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        browser.RefreshToken.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task CannotReadOrRevokeAnotherUsersSession()
    {
        var a = await LearnerAsync();
        var b = await LearnerAsync();
        using var first = Browser();
        using var second = Browser();
        string accessA = await AccessAsync(await LoginAsync(first, a.Email));
        string accessB = await AccessAsync(await LoginAsync(second, b.Email));
        Guid familyB = Guid.Parse(new JsonWebToken(accessB).GetClaim("sid").Value);
        (await second.SendAsync(HttpMethod.Get, $"/api/v1/me/sessions/{familyB}", bearer: accessB)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.SendAsync(HttpMethod.Get, $"/api/v1/me/sessions/{familyB}", bearer: accessA)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var listed = await first.SendAsync(HttpMethod.Get, "/api/v1/me/sessions", bearer: accessA);
        (await listed.Content.ReadAsStringAsync()).Should().NotContain(familyB.ToString());
        (await first.SendAsync(HttpMethod.Delete, $"/api/v1/me/sessions/{familyB}", bearer: accessA)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await second.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: accessB)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RevokeAllInvalidatesBothDevices()
    {
        var learner = await LearnerAsync();
        using var a = Browser();
        using var b = Browser();
        string first = await AccessAsync(await LoginAsync(a, learner.Email));
        string second = await AccessAsync(await LoginAsync(b, learner.Email));
        (await a.SendAsync(HttpMethod.Delete, "/api/v1/me/sessions", bearer: first)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await b.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: second)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PrivilegedRoleCannotBypassTwoFactorRequirement()
    {
        var learner = await LearnerAsync();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await users.AddToRoleAsync((await users.FindByIdAsync(learner.Id.ToString()))!, RoleNames.Admin);
        }
        using var browser = Browser();
        (await LoginAsync(browser, learner.Email)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        browser.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task BlockingAccountInvalidatesAccessAndRefresh()
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        string access = await AccessAsync(await LoginAsync(browser, learner.Email));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await database.Users.Where(user => user.Id == learner.Id).ExecuteUpdateAsync(update => update.SetProperty(user => user.Status, "Blocked"));
        }
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: access)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", csrf: true)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("wrong-issuer", "LexiGo.Web", false)]
    [InlineData("LexiGo", "wrong-audience", false)]
    [InlineData("LexiGo", "LexiGo.Web", true)]
    public async Task InvalidJwtClaimsAreRejected(string issuer, string audience, bool expired)
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        string original = await AccessAsync(await LoginAsync(browser, learner.Email));
        var jwt = new JsonWebToken(original);
        var key = fixture.Services.GetRequiredService<SigningKeyProvider>();
        DateTime now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = now.AddHours(-1),
            IssuedAt = now.AddHours(-1),
            Expires = expired ? now.AddMinutes(-1) : now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key.Key, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object> { ["sub"] = learner.Id.ToString(), ["sid"] = jwt.GetClaim("sid").Value, ["role"] = new[] { RoleNames.Learner }, ["amr"] = new[] { "pwd" } }
        };
        string altered = new JsonWebTokenHandler().CreateToken(descriptor);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: altered)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtSignatureTamperingIsRejected()
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        string original = await AccessAsync(await LoginAsync(browser, learner.Email));
        int index = original.LastIndexOf('.') + 8;
        string altered = original[..index] + (original[index] == 'A' ? 'B' : 'A') + original[(index + 1)..];
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: altered)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
