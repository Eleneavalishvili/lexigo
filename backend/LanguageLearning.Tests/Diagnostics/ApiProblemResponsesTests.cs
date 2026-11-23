using System.Net;
using System.Text.Json;
using LanguageLearning.Tests.Accounts;

namespace LanguageLearning.Tests.Diagnostics;

public sealed class ApiProblemResponsesTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task AuthenticationChallengeHasProblemDetailsAndRetainsBearerHeader()
    {
        using var response = await fixture.Client.GetAsync("/api/v1/me/dashboard");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
    }

    [Fact]
    public async Task LearnerDeniedAdminAccessReceivesProblemDetails()
    {
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await learner.LoginAsync(browser);
        using var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/users", bearer: token);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnmappedApiRouteHasProblemDetailsWithoutEchoingUrl()
    {
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await learner.LoginAsync(browser);
        using var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/unknown?secret=private-query", bearer: token);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.DoesNotContain("private-query", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InvalidPageRetainsFieldValidationErrors()
    {
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await learner.LoginAsync(browser);
        using var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/classes?page=0", bearer: token);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("page", out _));
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }
}
