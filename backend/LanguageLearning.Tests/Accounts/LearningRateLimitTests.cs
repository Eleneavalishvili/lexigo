using System.Net;
using System.Text.Json;

namespace LanguageLearning.Tests.Accounts;

public sealed class LearningRateLimitTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task LearnersOnTheSameConnectionHaveSeparateRequestBudgets()
    {
        var first = await SecurityTestAccount.CreateAsync(fixture);
        var second = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateRateLimitedClient("LearningRateLimit"));
        string firstToken = await first.LoginAsync(browser);
        string secondToken = await second.LoginAsync(browser);
        const string dashboard = "/api/v1/me/dashboard";

        using var firstRequest = await browser.SendAsync(HttpMethod.Get, dashboard, bearer: firstToken);
        using var rejected = await browser.SendAsync(HttpMethod.Get, dashboard, bearer: firstToken);
        using var secondRequest = await browser.SendAsync(HttpMethod.Get, dashboard, bearer: secondToken);

        Assert.Equal(HttpStatusCode.OK, firstRequest.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal(HttpStatusCode.OK, secondRequest.StatusCode);
    }
}
