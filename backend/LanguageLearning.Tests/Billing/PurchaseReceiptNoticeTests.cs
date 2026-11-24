using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Billing;

public sealed partial class SubscriptionPurchaseTests
{
    [Fact]
    public async Task SuccessfulPurchaseQueuesOneReceiptAndPremiumSurvivesReplayAndReload()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var buyer = await Setup(browser);
        var quote = await Read<BillingOrderView>(await Order(browser, buyer));
        var key = Guid.NewGuid();
        var payment = await Read<PaymentView>(await Pay(browser, buyer, quote.Id, key));
        Assert.Equal("Succeeded", payment.Status);
        Assert.StartsWith("LXG-", payment.ReceiptNumber);
        Assert.Equal(payment, await Read<PaymentView>(await Pay(browser, buyer, quote.Id, key)));
        var access = await Read<EntitlementView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/entitlements", bearer: buyer.Token));
        Assert.Equal("PREMIUM", access.PlanCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var receipt = await database.EmailOutboxMessages.SingleAsync(message => message.UserId == buyer.User.Id && message.Purpose == "BillingReceipt");
        Assert.Equal(buyer.User.Email, receipt.Recipient);
        Assert.Contains(payment.ReceiptNumber!, receipt.HtmlBody);
        Assert.Contains("/billing/receipt/" + payment.Id, receipt.HtmlBody);
        Assert.Equal(1, await database.Notifications.CountAsync(item => item.UserId == buyer.User.Id && item.Type == "SubscriptionActivated"));
        using var other = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var stranger = await Setup(other);
        using var denied = await other.SendAsync(HttpMethod.Get, $"/api/v1/me/payments/{payment.Id}", bearer: stranger.Token);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task CardCreationStoresOnlyFourDigitsAndPreservesIdempotency()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var buyer = await Setup(browser);
        var input = new { nickname = "My card", currency = "GEL", displayLastFour = "6789", idempotencyKey = Guid.NewGuid() };
        var first = await Read<CardChange>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards", input, buyer.Token));
        var replay = await Read<CardChange>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards", input, buyer.Token));
        Assert.Equal(first, replay);
        Assert.Equal("6789", first.Card.DisplayLastFour);
        Assert.Equal(0, first.Card.Balance);
        using var fullNumber = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards",
            new { nickname = "Rejected", currency = "GEL", displayLastFour = "1234567890123456", idempotencyKey = Guid.NewGuid() }, buyer.Token);
        Assert.Equal(HttpStatusCode.BadRequest, fullNumber.StatusCode);
    }
}
