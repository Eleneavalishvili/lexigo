using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Tests.Accounts;

namespace LanguageLearning.Tests.Content;

public sealed partial class RewardCatalogTests
{
    [Theory]
    [InlineData("achievements")]
    [InlineData("challenges")]
    public async Task RewardCreationHasPrivateLocationsAndSearchPagesHaveExactFilteredCounts(string resource)
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var staff = await StaffAsync(browser, "Admin");
        string prefix = "paging-" + Guid.NewGuid().ToString("N");
        string route = "/api/v1/manage/" + resource;
        for (int index = 0; index < 3; index++)
        {
            object input = resource == "achievements" ? Badge(prefix + index) with { Title = prefix + index }
                : FutureChallenge() with { Title = prefix + index };
            using var created = await browser.SendAsync(HttpMethod.Post, route, input, staff.Token);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var body = (await created.Content.ReadFromJsonAsync<CreatedReward>())!;
            Assert.Equal($"{route}/{body.Id}", created.Headers.Location!.AbsolutePath);
            using var detail = await browser.SendAsync(HttpMethod.Get, created.Headers.Location.PathAndQuery, bearer: staff.Token);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var hidden = await fixture.Client.GetAsync(created.Headers.Location.PathAndQuery);
            Assert.Equal(HttpStatusCode.Unauthorized, hidden.StatusCode);
        }
        string query = $"{route}?search={prefix}&pageSize=2";
        var first = await Read<CatalogPage<JsonElement>>(await browser.SendAsync(HttpMethod.Get, query, bearer: staff.Token));
        var second = await Read<CatalogPage<JsonElement>>(await browser.SendAsync(HttpMethod.Get, query + "&page=2", bearer: staff.Token));
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(2, first.Items.Length);
        Assert.Single(second.Items);
        Assert.True(first.HasMore);
        Assert.False(second.HasMore);
        Assert.Equal(3, first.Items.Concat(second.Items).Select(item => item.GetProperty("id").GetGuid()).Distinct().Count());
        var empty = await Read<CatalogPage<JsonElement>>(await browser.SendAsync(HttpMethod.Get, query + "&page=99", bearer: staff.Token));
        Assert.Empty(empty.Items);
        Assert.Equal(3, empty.TotalCount);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, route + "?pageSize=101", bearer: staff.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, route + "?search=" + new string('x', 101), bearer: staff.Token)).StatusCode);
    }

    private sealed record CreatedReward(Guid Id);
}
