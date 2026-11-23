using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class NotificationTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task InboxUsesOwnedStableCursorPagesAndReadIsIdempotent()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var owner = await SecurityTestAccount.CreateAsync(fixture); var token = await owner.LoginAsync(browser);
        var other = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var created = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds());
        var owned = Enumerable.Range(0, 3).Select(_ => new Notification { UserId = owner.Id, Type = "SubscriptionEnded", Title = "A postcard", Body = "No funds charged.", CreatedAt = created }).ToArray();
        var foreign = new Notification { UserId = other.Id, Type = "Private", Title = "Another account", Body = "Must remain private", CreatedAt = created };
        db.Notifications.AddRange(owned.Append(foreign)); await db.SaveChangesAsync();
        var first = (await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/notifications?limit=2", bearer: token)).Content.ReadFromJsonAsync<NotificationPage>())!;
        Assert.Equal(2, first.Items.Length); Assert.NotNull(first.NextBefore); Assert.NotNull(first.NextId);
        var url = $"/api/v1/me/notifications?limit=2&before={Uri.EscapeDataString(first.NextBefore.Value.ToString("O"))}&beforeId={first.NextId}";
        var second = (await (await browser.SendAsync(HttpMethod.Get, url, bearer: token)).Content.ReadFromJsonAsync<NotificationPage>())!;
        Assert.Single(second.Items); Assert.Null(second.NextBefore); Assert.Null(second.NextId);
        Assert.Equal(3, first.Items.Concat(second.Items).Select(item => item.Id).Distinct().Count());
        Assert.DoesNotContain(first.Items.Concat(second.Items), item => item.Id == foreign.Id);
        var id = first.Items[0].Id;
        var read = await browser.SendAsync(HttpMethod.Patch, $"/api/v1/me/notifications/{id}/read", new { }, token);
        read.EnsureSuccessStatusCode(); var value = (await read.Content.ReadFromJsonAsync<NotificationView>())!;
        Assert.NotNull(value.ReadAt);
        var again = await browser.SendAsync(HttpMethod.Patch, $"/api/v1/me/notifications/{id}/read", new { }, token);
        Assert.Equal(value, await again.Content.ReadFromJsonAsync<NotificationView>());
        Assert.Equal(HttpStatusCode.NotFound, (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/me/notifications/{foreign.Id}/read", new { }, token)).StatusCode);
        Assert.Null((await db.Notifications.AsNoTracking().SingleAsync(item => item.Id == foreign.Id)).ReadAt);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.SendAsync(HttpMethod.Patch, $"/api/v1/me/notifications/{Guid.NewGuid()}/read", new { }, token)).StatusCode);
    }

    [Fact]
    public async Task InboxRequiresAuthenticationAndValidBoundedPairedCursors()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/notifications")).StatusCode);
        var owner = await SecurityTestAccount.CreateAsync(fixture); var token = await owner.LoginAsync(browser);
        foreach (var query in new[] { "limit=0", "limit=51", "beforeId=" + Guid.NewGuid(), "before=2026-10-29T00:00:00Z" })
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/notifications?" + query, bearer: token)).StatusCode);
        var offsetCursor = Uri.EscapeDataString("2026-10-29T14:00:00+04:00");
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/notifications?before={offsetCursor}&beforeId={Guid.NewGuid()}", bearer: token)).StatusCode);
    }
}
