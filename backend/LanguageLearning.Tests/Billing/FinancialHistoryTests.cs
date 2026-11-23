using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Billing;

public sealed class FinancialHistoryTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private sealed record State(SecurityTestAccount User, string Token, Guid CardId, BillingOrderView Order);
    private async Task<State> Setup(SessionTestBrowser browser)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var token = await user.LoginAsync(browser);
        var card = await Read<CardChange>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards",
            new { nickname = "History", currency = "GEL", idempotencyKey = Guid.NewGuid() }, token));
        var order = await Read<BillingOrderView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/billing-orders",
            new { planPriceId = BillingCatalogSeed.Monthly, idempotencyKey = Guid.NewGuid() }, token));
        return new(user, token, card.Card.Id, order);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static Task<HttpResponseMessage> Payment(SessionTestBrowser browser, State state) => browser.SendAsync(HttpMethod.Post,
        $"/api/v1/me/billing-orders/{state.Order.Id}/payments", new { testCardId = state.CardId, idempotencyKey = Guid.NewGuid() }, state.Token);
    private static async Task<BillingActivityPage> History(SessionTestBrowser browser, State state, string query = "") =>
        await Read<BillingActivityPage>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/transactions" + query, bearer: state.Token));

    [Fact]
    public async Task FailedAndSuccessfulAttemptsShareHistoryWithoutFabricatingALedgerDebit()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        var failed = await Read<PaymentView>(await Payment(browser, state)); Assert.Equal("Failed", failed.Status);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{state.CardId}/top-ups", new { amount = 20, idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        var paid = await Read<PaymentView>(await Payment(browser, state)); Assert.Equal("Succeeded", paid.Status);
        var history = await History(browser, state);
        Assert.Equal(3, history.TotalCount);
        var declined = Assert.Single(history.Items, item => item.Status == "Failed");
        Assert.Equal(failed.Id, declined.PaymentId); Assert.Equal(state.Order.Id, declined.OrderId);
        Assert.Equal("InsufficientBalance", declined.FailureCode); Assert.Null(declined.BalanceAfter);
        Assert.Equal("PaymentAttempt", declined.EntryKind); Assert.Equal("PAY-" + failed.Id.ToString("N"), declined.Reference);
        var success = Assert.Single(history.Items, item => item.PaymentId == paid.Id);
        Assert.Equal("Succeeded", success.Status); Assert.Equal(10.01m, success.BalanceAfter); Assert.Equal(state.Order.Id, success.OrderId);
        Assert.Equal(2, (await History(browser, state, "?status=Succeeded")).TotalCount);
        Assert.Equal(1, (await History(browser, state, "?status=Failed&type=Debit")).TotalCount);
        Assert.Equal(1, (await History(browser, state, "?type=TopUp")).TotalCount);
        Assert.Empty((await History(browser, state, "?status=Pending")).Items);
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-1).ToOffset(TimeSpan.FromHours(4)).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"));
        Assert.Equal(3, (await History(browser, state, $"?cardId={state.CardId}&from={from}&to={to}")).TotalCount);
        Assert.Empty((await History(browser, state, "?to=2000-01-01T00:00:00Z")).Items);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(2, await db.CardTransactions.CountAsync(item => item.TestCardId == state.CardId));
        Assert.False(await db.CardTransactions.AnyAsync(item => item.PaymentId == failed.Id));
    }

    [Fact]
    public async Task HeartRefillFailuresAreVisibleWithoutShowingAnotherAccountsRecords()
    {
        using var owner = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(owner);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var refillAt = DateTimeOffset.UtcNow.AddHours(1);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"LearnerHearts\" (\"UserId\",\"Count\",\"RefillAt\") VALUES ({state.User.Id},0,{refillAt}) ON CONFLICT (\"UserId\") DO UPDATE SET \"Count\" = 0,\"RefillAt\" = EXCLUDED.\"RefillAt\"");
        (await owner.SendAsync(HttpMethod.Post, "/api/v1/me/hearts/purchases", new { testCardId = state.CardId, offerCode = HeartPurchaseService.OfferCode, idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        var history = await History(owner, state, "?status=Failed");
        var failed = Assert.Single(history.Items); Assert.Equal("HeartRefillPurchase", failed.EntryKind);
        Assert.NotNull(failed.HeartRefillPurchaseId); Assert.Null(failed.PaymentId); Assert.Null(failed.OrderId); Assert.Null(failed.BalanceAfter);
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient()); var other = await Setup(outsider);
        Assert.Empty((await History(outsider, other)).Items);
        var export = await Read<System.Text.Json.JsonElement>(
            await owner.SendAsync(HttpMethod.Get, "/api/v1/me/export", bearer: state.Token));
        var refill = Assert.Single(export.GetProperty("simulatedBilling").GetProperty("heartRefills").EnumerateArray());
        Assert.Equal("Failed", refill.GetProperty("status").GetString());
        Assert.Equal(state.CardId, refill.GetProperty("testCardId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, $"/api/v1/me/transactions?cardId={state.CardId}", bearer: other.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, $"/api/v1/me/test-cards/{state.CardId}/transactions", bearer: other.Token)).StatusCode);
        Assert.False(await db.CardTransactions.AnyAsync(item => item.TestCardId == state.CardId));
    }

    [Fact]
    public async Task PendingAttemptsPageDeterministicallyAndFiltersAreBounded()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.PaymentAttempts.AddRange(Enumerable.Range(0, 21).Select(_ => new PaymentAttempt
        {
            UserId = state.User.Id,
            OrderId = state.Order.Id,
            TestCardId = state.CardId,
            Status = "Pending",
            Amount = state.Order.Amount,
            Currency = state.Order.Currency,
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = now
        }));
        await db.SaveChangesAsync();
        var first = await History(browser, state, "?status=Pending&page=1");
        var second = await History(browser, state, "?status=Pending&page=2");
        Assert.Equal(21, first.TotalCount); Assert.Equal(20, first.Items.Count); Assert.Single(second.Items);
        Assert.Equal(21, first.Items.Concat(second.Items).Select(item => item.Id).Distinct().Count());
        Assert.All(first.Items, item => { Assert.Null(item.BalanceAfter); Assert.Equal("Pending", item.Status); });
        foreach (var query in new[] { "?status=Unknown", "?page=0", "?page=100001", "?from=2026-10-30&to=2026-10-29", "?type=Unknown" })
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/transactions" + query, bearer: state.Token)).StatusCode);
    }
}
