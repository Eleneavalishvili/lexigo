using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class ActivityTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string Route = "/api/v1/me/activity";

    [Fact]
    public async Task DateWindowAndPagingReadOnlyTheSignedInLearnersDays()
    {
        var owner = await SecurityTestAccount.CreateAsync(fixture);
        var other = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await owner.LoginAsync(browser);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var start = new DateOnly(2026, 10, 26);
        database.DailyActivities.AddRange(
            Day(owner.Id, start.AddDays(-1), 20), Day(owner.Id, start, 20),
            Day(other.Id, start.AddDays(1), 99), Day(owner.Id, start.AddDays(2), 40),
            Day(owner.Id, start.AddDays(3), 60));
        await database.SaveChangesAsync();

        const string window = Route + "?from=2026-10-26&to=2026-10-28&pageSize=1";
        var first = await Read(window + $"&userId={other.Id}");
        Assert.Equal(new ActivityDay(start, 20, 1, true), Assert.Single(first.Items));
        Assert.Equal(1, first.Page);
        Assert.Equal(1, first.PageSize);
        Assert.Equal(2, first.TotalCount);
        var second = await Read(window + "&page=2");
        Assert.Equal(new ActivityDay(start.AddDays(2), 40, 1, true), Assert.Single(second.Items));
        var outside = await Read(window + "&page=3");
        Assert.Empty(outside.Items);
        Assert.Equal(2, outside.TotalCount);

        async Task<ActivityPage> Read(string path)
        {
            using var response = await browser.SendAsync(HttpMethod.Get, path, bearer: token);
            response.EnsureSuccessStatusCode();
            Assert.True(response.Headers.CacheControl?.NoStore);
            return (await response.Content.ReadFromJsonAsync<ActivityPage>())!;
        }
    }

    [Fact]
    public async Task RejectsMissingReversedOrOversizedDateWindowsAndInvalidPageSizes()
    {
        var owner = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await owner.LoginAsync(browser);
        string[] invalid = ["?from=2026-10-26", "?from=not-a-date&to=2026-10-28",
            "?from=2026-10-28&to=2026-10-26", "?from=2026-10-26&to=2028-10-26",
            "?from=2026-10-26&to=2026-10-28&pageSize=101"];

        foreach (string query in invalid)
        {
            using var response = await browser.SendAsync(HttpMethod.Get, Route + query, bearer: token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private static DailyActivity Day(Guid userId, DateOnly date, int earnedXp) => new()
    {
        UserId = userId, LocalDate = date, EarnedXp = earnedXp, CompletedLessons = 1, GoalReached = true
    };
}
