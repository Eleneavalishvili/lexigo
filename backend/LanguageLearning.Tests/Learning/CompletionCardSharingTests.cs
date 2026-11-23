using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed partial class CompletionCardTests
{
    [Fact]
    public async Task SharingRequiresConsentRotatesAfterRevocationAndNeverExposesPrivateIdentifiers()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(browser);
        await FinishAsync(browser, setup.Token, setup.Content.LessonId);
        var card = await CardAsync(browser, setup.Token, setup.Unit);
        Assert.Equal(HttpStatusCode.Conflict, (await ShareAsync(browser, setup.Token, card, true, consent: false)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ShareAsync(browser, setup.Token, card, true)).StatusCode);
        var enabled = await CardAsync(browser, setup.Token, setup.Unit);
        var response = await fixture.Client.GetAsync("/api/v1/completion-cards/shared/" + enabled.ShareCode);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("noindex", string.Join(",", response.Headers.GetValues("X-Robots-Tag")));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (string field in new[] { "userId", "courseId", "unitId", "email", "shareCode", "revision" })
            Assert.False(json.RootElement.TryGetProperty(field, out _));
        Assert.Equal(card.Details.VerificationCode, json.RootElement.GetProperty("verificationCode").GetString());
        Assert.Equal(HttpStatusCode.OK, (await ShareAsync(browser, setup.Token, enabled, false, consent: false)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/api/v1/completion-cards/shared/" + enabled.ShareCode)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ShareAsync(browser, setup.Token, enabled, true)).StatusCode);
        var disabled = await CardAsync(browser, setup.Token, setup.Unit);
        Assert.False(disabled.IsShared);
        Assert.Null(disabled.ShareCode);
        Assert.Equal(HttpStatusCode.OK, (await ShareAsync(browser, setup.Token, disabled, true)).StatusCode);
        var renewed = await CardAsync(browser, setup.Token, setup.Unit);
        Assert.NotEqual(enabled.ShareCode, renewed.ShareCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/api/v1/completion-cards/shared/" + enabled.ShareCode)).StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Users.Where(item => item.Id == setup.Account.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.DisplayName, "Updated fixture name"));
        var publicCard = (await fixture.Client.GetFromJsonAsync<CompletionCardDetails>("/api/v1/completion-cards/shared/" + renewed.ShareCode))!;
        Assert.Equal("Updated fixture name", publicCard.DisplayName);
        await db.Users.Where(item => item.Id == setup.Account.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Blocked"));
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/api/v1/completion-cards/shared/" + renewed.ShareCode)).StatusCode);
    }

    [Fact]
    public async Task ForeignAccountsCannotReadListOrChangeAnotherLearnersCard()
    {
        using var owner = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(owner);
        await FinishAsync(owner, setup.Token, setup.Content.LessonId);
        var card = await CardAsync(owner, setup.Token, setup.Unit);
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var other = await SecurityTestAccount.CreateAsync(fixture);
        string token = await other.LoginAsync(outsider);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/v1/me/completion-cards")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, "/api/v1/me/completion-cards/" + card.Id, bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ShareAsync(outsider, token, card, true)).StatusCode);
        var list = await outsider.SendAsync(HttpMethod.Get, "/api/v1/me/completion-cards?userId=" + setup.Account.Id, bearer: token);
        list.EnsureSuccessStatusCode();
        Assert.Empty((await list.Content.ReadFromJsonAsync<CompletionCardPage>())!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/api/v1/completion-cards/shared/not-a-code")).StatusCode);
    }
}
