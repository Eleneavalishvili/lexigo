using System.Net;

namespace LanguageLearning.Tests.Accounts;

public sealed class StudyGoalRateLimitTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task StudyGoalBudgetIsPerAccountAndDoesNotConsumeAuthenticationBudget()
    {
        var a = await SecurityTestAccount.CreateAsync(fixture);
        var b = await SecurityTestAccount.CreateAsync(fixture);
        using var normalA = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var normalB = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var tokenA = await a.LoginAsync(normalA);
        var tokenB = await b.LoginAsync(normalB);
        using var limited = new SessionTestBrowser(fixture.CreateRateLimitedClient("AccountSettingsRateLimit"));
        using var first = await limited.SendAsync(HttpMethod.Put, "/api/v1/me/study-goal", new { dailyGoalXp = 20 }, tokenA);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using var repeated = await limited.SendAsync(HttpMethod.Put, "/api/v1/me/study-goal", new { dailyGoalXp = 40 }, tokenA);
        Assert.Equal(HttpStatusCode.TooManyRequests, repeated.StatusCode);
        using var other = await limited.SendAsync(HttpMethod.Put, "/api/v1/me/study-goal", new { dailyGoalXp = 20 }, tokenB);
        Assert.Equal(HttpStatusCode.NoContent, other.StatusCode);
        using var reset = await limited.SendAsync(HttpMethod.Post, "/api/v1/auth/forgot-password", new { email = "unknown@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, reset.StatusCode);
    }
}
