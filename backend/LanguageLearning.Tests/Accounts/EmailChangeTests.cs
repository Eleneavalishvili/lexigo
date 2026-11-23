using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Tests.Accounts;

public sealed class EmailChangeTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string Password = "Email change test passphrase 42!";
    private static string Email() => $"change-{Guid.NewGuid():N}@example.test";

    private async Task<(Guid Id, string Email, string Access, SessionTestBrowser Browser)> LearnerAsync()
    {
        string email = Email();
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = "Email Learner", password = Password })).EnsureSuccessStatusCode();
        var confirmation = await fixture.ConfirmationAsync(email);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = confirmation.UserId, token = confirmation.Token })).EnsureSuccessStatusCode();
        var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string access = await LoginAsync(browser, email);
        return (confirmation.UserId, email, access, browser);
    }

    private static async Task<string> LoginAsync(SessionTestBrowser browser, string email, string? recoveryCode = null)
    {
        var result = await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password = Password, recoveryCode }, csrf: true);
        result.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<object> LinkAsync(Guid userId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var message = await database.EmailOutboxMessages.Where(item => item.UserId == userId && item.Purpose == "EmailChange")
            .OrderByDescending(item => item.CreatedAt).FirstAsync();
        string requestId = Regex.Match(message.HtmlBody, "requestId=([a-f0-9-]+)").Groups[1].Value;
        string token = Regex.Match(message.HtmlBody, "token=([A-Za-z0-9_-]+)").Groups[1].Value;
        requestId.Should().NotBeEmpty(); token.Should().NotBeEmpty();
        return new { userId, requestId, token };
    }

    [Fact]
    public async Task RequestDoesNotChangeEmailAndConfirmationRevokesAllSessions()
    {
        var learner = await LearnerAsync(); using var browser = learner.Browser;
        string target = Email();
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = target, currentPassword = Password }, learner.Access)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            (await database.Users.SingleAsync(item => item.Id == learner.Id)).Email.Should().Be(learner.Email);
            string stored = (await database.UserTokens.SingleAsync(item => item.UserId == learner.Id && item.Name == "PendingEmailChange")).Value!;
            stored.Should().NotContain(target);
        }
        object link = await LinkAsync(learner.Id);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", link)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", link)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: learner.Access)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", new { }, csrf: true)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _ = await LoginAsync(browser, target);
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var finalDatabase = finalScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var user = await finalDatabase.Users.SingleAsync(item => item.Id == learner.Id);
        user.EmailConfirmed.Should().BeTrue(); user.UserName.Should().Be(target);
        var notices = await finalDatabase.EmailOutboxMessages.Where(item => item.UserId == learner.Id && item.Purpose == "SecurityNotification").Select(item => item.Recipient).ToArrayAsync();
        notices.Should().BeEquivalentTo(new[] { learner.Email, target });
    }

    [Fact]
    public async Task MissingProofAnonymousAndOverpostingAreRejected()
    {
        var learner = await LearnerAsync(); using var browser = learner.Browser;
        var body = new { newEmail = Email(), currentPassword = "Wrong password 42!" };
        (await fixture.Client.PutAsJsonAsync("/api/v1/me/email", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", body, learner.Access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = Email(), currentPassword = Password, userId = Guid.NewGuid() }, learner.Access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = fixture.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().EmailOutboxMessages.CountAsync(item => item.UserId == learner.Id && item.Purpose == "EmailChange")).Should().Be(0);
    }

    [Fact]
    public async Task OccupiedTargetAndCooldownHaveSameAcceptedBody()
    {
        var learner = await LearnerAsync(); using var browser = learner.Browser;
        var other = await LearnerAsync(); using var otherBrowser = other.Browser;
        var occupied = await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = other.Email, currentPassword = Password }, learner.Access);
        var valid = await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = Email(), currentPassword = Password }, learner.Access);
        var cooldown = await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = Email(), currentPassword = Password }, learner.Access);
        occupied.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await valid.Content.ReadAsStringAsync()).Should().Be(await occupied.Content.ReadAsStringAsync());
        (await cooldown.Content.ReadAsStringAsync()).Should().Be(await occupied.Content.ReadAsStringAsync());
        await using var scope = fixture.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().EmailOutboxMessages.CountAsync(item => item.UserId == learner.Id && item.Purpose == "EmailChange")).Should().Be(1);
    }

    [Fact]
    public async Task ExpiredTamperedAndWrongUserTokensCannotChangeEmail()
    {
        var learner = await LearnerAsync(); using var browser = learner.Browser;
        await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = Email(), currentPassword = Password }, learner.Access);
        object link = await LinkAsync(learner.Id);
        var fields = JsonSerializer.SerializeToElement(link);
        foreach (object invalid in new object[] {
            new { userId = Guid.NewGuid(), requestId = fields.GetProperty("requestId").GetString(), token = fields.GetProperty("token").GetString() },
            new { userId = learner.Id, requestId = fields.GetProperty("requestId").GetString(), token = "tampered" } })
            (await fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var options = fixture.Services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;
        TimeSpan original = options.TokenLifespan;
        try { options.TokenLifespan = TimeSpan.Zero; (await fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", link)).StatusCode.Should().Be(HttpStatusCode.BadRequest); }
        finally { options.TokenLifespan = original; }
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: learner.Access)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConcurrentConfirmationSucceedsOnlyOnce()
    {
        var learner = await LearnerAsync(); using var browser = learner.Browser;
        await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = Email(), currentPassword = Password }, learner.Access);
        object link = await LinkAsync(learner.Id);
        var responses = await Task.WhenAll(fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", link), fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", link));
        responses.Count(item => item.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(item => item.StatusCode == HttpStatusCode.BadRequest).Should().Be(1);
    }

    [Fact]
    public async Task NewRequestSupersedesAnOlderLinkEvenForTheSameTarget()
    {
        var learner = await LearnerAsync(); using var browser = learner.Browser;
        var body = new { newEmail = Email(), currentPassword = Password };
        await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", body, learner.Access);
        object old = await LinkAsync(learner.Id);
        await using (var scope = fixture.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().EmailOutboxMessages.Where(item => item.UserId == learner.Id && item.Purpose == "EmailChange")
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-6)));
        await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", body, learner.Access);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", old)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", await LinkAsync(learner.Id))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddressClaimedAfterRequestDoesNotOverwriteAnotherAccount()
    {
        var learner = await LearnerAsync(); using var browser = learner.Browser;
        string target = Email();
        await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = target, currentPassword = Password }, learner.Access);
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new { email = target, displayName = "Other Learner", password = Password });
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", await LinkAsync(learner.Id))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = fixture.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().Users.SingleAsync(item => item.Id == learner.Id)).Email.Should().Be(learner.Email);
    }

    [Fact]
    public async Task EnabledMfaRequiresFreshProofAndSurvivesEmailChange()
    {
        var learner = await LearnerAsync(); using var browser = learner.Browser;
        string[] recovery;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(learner.Id.ToString()))!;
            await users.ResetAuthenticatorKeyAsync(user);
            await users.SetTwoFactorEnabledAsync(user, true);
            recovery = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 4))!.ToArray();
        }
        string access = await LoginAsync(browser, learner.Email, recovery[0]);
        string target = Email();
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = target, currentPassword = Password }, access)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/email", new { newEmail = target, currentPassword = Password, recoveryCode = recovery[1] }, access)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", await LinkAsync(learner.Id))).StatusCode.Should().Be(HttpStatusCode.OK);
        _ = await LoginAsync(browser, target, recovery[2]);
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var manager = finalScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var changed = (await manager.FindByIdAsync(learner.Id.ToString()))!;
        changed.TwoFactorEnabled.Should().BeTrue();
        (await manager.RedeemTwoFactorRecoveryCodeAsync(changed, recovery[1])).Succeeded.Should().BeFalse();
    }
}
