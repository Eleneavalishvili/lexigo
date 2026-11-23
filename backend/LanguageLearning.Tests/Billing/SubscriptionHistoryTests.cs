using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Billing;

public sealed class SubscriptionHistoryTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private sealed record State(SecurityTestAccount User, string Token, Guid CardId, PaymentView Payment);
    private async Task<State> Setup(SessionTestBrowser browser)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture); var token = await user.LoginAsync(browser);
        var card = await Read<CardChange>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards", new { nickname = "History", currency = "GEL", idempotencyKey = Guid.NewGuid() }, token));
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{card.Card.Id}/top-ups", new { amount = 100, idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
        var payment = await Buy(browser, token, card.Card.Id);
        return new(user, token, card.Card.Id, payment);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static async Task<PaymentView> Buy(SessionTestBrowser browser, string token, Guid card)
    {
        var order = await Read<BillingOrderView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/billing-orders", new { planPriceId = BillingCatalogSeed.Monthly, idempotencyKey = Guid.NewGuid() }, token));
        return await Read<PaymentView>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/billing-orders/{order.Id}/payments", new { testCardId = card, idempotencyKey = Guid.NewGuid() }, token));
    }

    [Fact]
    public async Task OwnedEndedAndCurrentChaptersRetainOriginalPaymentTermsAndEvents()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        var id = state.Payment.SubscriptionId!.Value;
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/subscription/cancel", new { subscriptionId = id, idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/subscription/resume", new { subscriptionId = id, idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        var events = await Read<SubscriptionEventsPage>(await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/subscriptions/{id}/events", bearer: state.Token));
        Assert.Contains(events.Items, item => item.Type == "CancellationRequested"); Assert.Contains(events.Items, item => item.Type == "Resumed");
        var activation = Assert.Single(events.Items, item => item.Type == "Activated");
        Assert.Equal(state.Payment.Id, activation.PaymentId); Assert.Equal(9.99m, activation.Amount); Assert.Equal("GEL", activation.Currency);
        Assert.Equal(state.Payment.CreatedAt, activation.PaidPeriodStart); Assert.Equal(state.Payment.CreatedAt.AddMonths(1), activation.PaidPeriodEnd);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var end = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.Subscriptions.Where(item => item.Id == id).ExecuteUpdateAsync(update => update.SetProperty(item => item.CurrentPeriodStart, end.AddMonths(-1)).SetProperty(item => item.CurrentPeriodEnd, end));
        var expired = await Read<SubscriptionHistoryPage>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscriptions", bearer: state.Token));
        Assert.Equal("Expired", Assert.Single(expired.Items).Status); Assert.False(expired.Items[0].IsCurrent);
        var fresh = await Buy(browser, state.Token, state.CardId);
        var history = await Read<SubscriptionHistoryPage>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscriptions", bearer: state.Token));
        Assert.Equal(2, history.TotalCount); Assert.Single(history.Items, item => item.IsCurrent && item.Id == fresh.SubscriptionId);
        Assert.Contains(history.Items, item => item.Id == id && item.Status == "Expired");
        var oldEvents = await Read<SubscriptionEventsPage>(await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/subscriptions/{id}/events", bearer: state.Token));
        Assert.Equal(activation, Assert.Single(oldEvents.Items, item => item.Type == "Activated"));
        Assert.Contains(oldEvents.Items, item => item.Type == "Expired");
    }

    [Fact]
    public async Task PrivateHistoryDeniesForeignIdsAndBoundsBothPageEndpoints()
    {
        using var owner = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(owner);
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient()); var other = await SecurityTestAccount.CreateAsync(fixture); var token = await other.LoginAsync(outsider);
        var empty = await Read<SubscriptionHistoryPage>(await outsider.SendAsync(HttpMethod.Get, "/api/v1/me/subscriptions", bearer: token)); Assert.Empty(empty.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, $"/api/v1/me/subscriptions/{state.Payment.SubscriptionId}/events", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, $"/api/v1/me/subscriptions/{Guid.NewGuid()}/events", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await outsider.SendAsync(HttpMethod.Get, "/api/v1/me/subscriptions")).StatusCode);
        foreach (int page in new[] { 0, 100001 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.SendAsync(HttpMethod.Get, $"/api/v1/me/subscriptions?page={page}", bearer: state.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.SendAsync(HttpMethod.Get, $"/api/v1/me/subscriptions/{state.Payment.SubscriptionId}/events?page={page}", bearer: state.Token)).StatusCode);
        }
    }

    [Fact]
    public async Task DatabaseRejectsCrossSubscriptionAndUnsuccessfulRelatedPayments()
    {
        using var owner = new SessionTestBrowser(fixture.CreateIsolatedClient()); var first = await Setup(owner);
        using var otherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var second = await Setup(otherBrowser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        db.SubscriptionEvents.Add(new SubscriptionEvent { SubscriptionId = first.Payment.SubscriptionId!.Value, RelatedPaymentId = second.Payment.Id, EventType = "SupportNote", ActorId = first.User.Id, OccurredAt = DateTimeOffset.UtcNow });
        var foreign = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(foreign.InnerException).SqlState); db.ChangeTracker.Clear();
        var failed = new PaymentAttempt
        {
            UserId = first.User.Id,
            OrderId = first.Payment.OrderId,
            TestCardId = first.CardId,
            Amount = 9.99m,
            Currency = "GEL",
            Status = "Failed",
            FailureCode = "TechnicalFailure",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        db.PaymentAttempts.Add(failed);
        await db.SaveChangesAsync();
        db.SubscriptionEvents.Add(new SubscriptionEvent { SubscriptionId = first.Payment.SubscriptionId.Value, RelatedPaymentId = failed.Id, EventType = "SupportNote", ActorId = first.User.Id, OccurredAt = DateTimeOffset.UtcNow });
        var unsuccessful = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(unsuccessful.InnerException).SqlState);
    }
}
