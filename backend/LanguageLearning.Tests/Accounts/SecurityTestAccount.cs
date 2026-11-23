using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

internal sealed record SecurityTestAccount(Guid Id, string Email, string Key, string[] RecoveryCodes)
{
    public const string Password = "Security boundary test passphrase 42!";

    public static async Task<SecurityTestAccount> CreateAsync(RegistrationFixture fixture, int recoveryCount = 10)
    {
        string email = $"security-{Guid.NewGuid():N}@example.test";
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = "Security Learner", password = Password })).EnsureSuccessStatusCode();
        var confirmation = await fixture.ConfirmationAsync(email);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = confirmation.UserId, token = confirmation.Token })).EnsureSuccessStatusCode();
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(confirmation.UserId.ToString()))!;
        // Isolated test fixture setup; production enrollment is covered separately in TwoFactorTests.
        await users.ResetAuthenticatorKeyAsync(user);
        await users.SetTwoFactorEnabledAsync(user, true);
        string key = (await users.GetAuthenticatorKeyAsync(user))!;
        string[] recovery = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, recoveryCount))!.ToArray();
        return new(user.Id, email, key, recovery);
    }

    public async Task<string> LoginAsync(SessionTestBrowser browser, int recoveryIndex = 0)
    {
        var response = await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/login",
            new { email = Email, password = Password, recoveryCode = RecoveryCodes[recoveryIndex] }, csrf: true);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    public async Task ExpireFamilyAuthenticationAsync(RegistrationFixture fixture)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().RefreshSessions.Where(item => item.UserId == Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-6)));
    }
}
