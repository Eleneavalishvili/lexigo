using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Tests.Accounts;

public sealed class PasswordRecoveryTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string Password = "Original learner passphrase 42!";
    private const string Replacement = "Replacement learner passphrase 73!";
    private SessionTestBrowser Browser() => new(fixture.CreateIsolatedClient());

    private async Task<(Guid Id, string Email)> LearnerAsync(bool confirmed = true)
    {
        string email = $"recovery-{Guid.NewGuid():N}@example.test";
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = "Recovery Learner", password = Password })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var link = await fixture.ConfirmationAsync(email);
        if (confirmed) await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = link.UserId, token = link.Token });
        return (link.UserId, email);
    }

    private static Task<HttpResponseMessage> LoginAsync(SessionTestBrowser browser, string email, string password = Password) =>
        browser.SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password }, csrf: true);

    private static async Task<string> AccessAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<string> ResetTokenAsync(Guid id)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var message = await database.EmailOutboxMessages.SingleAsync(message => message.UserId == id && message.Purpose == "PasswordReset");
        return Regex.Match(message.HtmlBody, "token=([A-Za-z0-9_-]+)").Groups[1].Value;
    }

    [Fact]
    public async Task RequestIsGenericThrottledAndDoesNotChangePasswordOrSessions()
    {
        var learner = await LearnerAsync();
        using var browser = Browser();
        string access = await AccessAsync(await LoginAsync(browser, learner.Email));
        var known = await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = learner.Email });
        var unknown = await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "unknown@example.test" });
        known.StatusCode.Should().Be(HttpStatusCode.Accepted);
        unknown.StatusCode.Should().Be(known.StatusCode);
        (await known.Content.ReadAsStringAsync()).Should().Be(await unknown.Content.ReadAsStringAsync());
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = learner.Email });
        await ResetTokenAsync(learner.Id); // SingleAsync also verifies the per-purpose cooldown.
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: access)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(browser, learner.Email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetIsSingleUseAndInvalidatesEveryExistingDevice()
    {
        var learner = await LearnerAsync();
        using var first = Browser();
        using var second = Browser();
        string accessA = await AccessAsync(await LoginAsync(first, learner.Email));
        string accessB = await AccessAsync(await LoginAsync(second, learner.Email));
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = learner.Email });
        string token = await ResetTokenAsync(learner.Id);
        var request = new { userId = learner.Id, token, newPassword = Replacement };
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", request)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await first.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: accessA)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await second.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: accessB)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await second.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", csrf: true)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(first, learner.Email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(first, learner.Email, Replacement)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetCannotChangeAnotherUserOrAcceptWeakPasswordOrMalformedToken()
    {
        var learner = await LearnerAsync();
        var other = await LearnerAsync();
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = learner.Email });
        string token = await ResetTokenAsync(learner.Id);
        foreach (var request in new[]
        {
            new { userId = other.Id, token, newPassword = Replacement },
            new { userId = learner.Id, token = "not-a-valid-token", newPassword = Replacement },
            new { userId = learner.Id, token, newPassword = "weak" },
            new { userId = learner.Id, token, newPassword = "long lowercase password!" },
            new { userId = learner.Id, token, newPassword = "Long password without symbol" },
            new { userId = learner.Id, token, newPassword = "Long password with spaces    " }
        })
            (await fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var browser = Browser();
        (await LoginAsync(browser, learner.Email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetDoesNotBypassEmailConfirmation()
    {
        var learner = await LearnerAsync(false);
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = learner.Email });
        string token = await ResetTokenAsync(learner.Id);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId = learner.Id, token, newPassword = Replacement })).StatusCode.Should().Be(HttpStatusCode.OK);
        using var browser = Browser();
        (await LoginAsync(browser, learner.Email, Replacement)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredResetTokenIsRejected()
    {
        var learner = await LearnerAsync();
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = learner.Email });
        string token = await ResetTokenAsync(learner.Id);
        var options = fixture.Services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;
        TimeSpan original = options.TokenLifespan;
        try
        {
            options.TokenLifespan = TimeSpan.Zero;
            (await fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId = learner.Id, token, newPassword = Replacement })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally { options.TokenLifespan = original; }
    }

    [Fact]
    public async Task ConcurrentResetRequestsOnlySucceedOnce()
    {
        var learner = await LearnerAsync();
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = learner.Email });
        string token = await ResetTokenAsync(learner.Id);
        var body = new { userId = learner.Id, token, newPassword = Replacement };
        var results = await Task.WhenAll(
            fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", body),
            fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", body));
        results.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Count(response => response.StatusCode == HttpStatusCode.BadRequest).Should().Be(1);
    }

    [Fact]
    public async Task PasswordChangeRequiresCurrentPasswordAndSignsOutAllDevices()
    {
        var learner = await LearnerAsync();
        using var first = Browser();
        using var second = Browser();
        string access = await AccessAsync(await LoginAsync(first, learner.Email));
        string other = await AccessAsync(await LoginAsync(second, learner.Email));
        (await first.SendAsync(HttpMethod.Put, "/api/v1/me/password", new { currentPassword = "Incorrect password 99!", newPassword = Replacement }, access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await first.SendAsync(HttpMethod.Put, "/api/v1/me/password", new { currentPassword = Password, newPassword = "Missing symbol password" }, access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await first.SendAsync(HttpMethod.Put, "/api/v1/me/password", new { currentPassword = Password, newPassword = "missing uppercase password!" }, access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: other)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.SendAsync(HttpMethod.Put, "/api/v1/me/password", new { currentPassword = Password, newPassword = Replacement }, access)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: other)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(first, learner.Email, Replacement)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
