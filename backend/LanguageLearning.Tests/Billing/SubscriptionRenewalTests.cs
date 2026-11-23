using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LanguageLearning.Tests.Billing;

public sealed class SubscriptionRenewalTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    private sealed record State(SecurityTestAccount User, string Token, Guid CardId, SubscriptionView Subscription);
    private async Task<State> Setup(SessionTestBrowser browser, bool consent = true)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var token = await user.LoginAsync(browser);
        var card = await Read<CardChange>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards",
            new { nickname = "Renewal test", currency = "GEL", idempotencyKey = Guid.NewGuid() }, token));
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{card.Card.Id}/top-ups", new { amount = 100, idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
        var order = await Read<BillingOrderView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/billing-orders",
            new { planPriceId = BillingCatalogSeed.Monthly, idempotencyKey = Guid.NewGuid() }, token));
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/billing-orders/{order.Id}/payments",
            new { testCardId = card.Card.Id, idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
        var subscription = await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: token));
        if (consent) subscription = await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Put, "/api/v1/me/subscription/renewal",
            new { subscriptionId = subscription.Id, autoRenew = true, defaultTestCardId = card.Card.Id, idempotencyKey = Guid.NewGuid() }, token));
        return new(user, token, card.Card.Id, subscription);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private async Task<int> Run(DateTimeOffset at, bool enabled = true)
    {
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        return await new SubscriptionRenewalProcessor(database, new SubscriptionPaymentRecorder(database), new Clock(at),
            Options.Create(new BillingRenewalOptions { Enabled = enabled }), NullLogger<SubscriptionRenewalProcessor>.Instance).ProcessDueAsync(default);
    }

    [Fact]
    public async Task HostedWorkerActuallyProcessesADuePeriodWithoutAClientPaymentRequest()
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System, poll: true));
        var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var end = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.Subscriptions.Where(item => item.Id == state.Subscription.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.CurrentPeriodStart, end.AddMonths(-1)).SetProperty(item => item.CurrentPeriodEnd, end));
        await db.BillingPreferences.Where(item => item.UserId == state.User.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RenewalConsentAt, end.AddMinutes(-1)));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline && !await db.BillingJobExecutions.AnyAsync(item => item.SubscriptionId == state.Subscription.Id))
            await Task.Delay(250);
        Assert.Equal("Succeeded", (await db.BillingJobExecutions.SingleAsync(item => item.SubscriptionId == state.Subscription.Id)).Status);
        Assert.Equal(80.02m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
    }

    [Fact]
    public async Task ConcurrentWorkersRenewOneCalendarPeriodWithOneDebitReceiptEventAndNotice()
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System));
        var state = await Setup(browser);
        Assert.True(state.Subscription.RenewalProcessingAvailable);
        Assert.False(state.Subscription.RenewalConsentRequired);
        var end = state.Subscription.CurrentPeriodEnd!.Value;
        await Task.WhenAll(Run(end), Run(end));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var current = await db.Subscriptions.SingleAsync(item => item.Id == state.Subscription.Id);
        Assert.Equal(end, current.CurrentPeriodStart); Assert.Equal(end.AddMonths(1), current.CurrentPeriodEnd);
        Assert.Equal(80.02m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.Single(await db.BillingJobExecutions.Where(item => item.SubscriptionId == current.Id).ToArrayAsync());
        var order = await db.BillingOrders.SingleAsync(item => item.SubscriptionId == current.Id && item.OrderType == "Renewal");
        var payment = await db.PaymentAttempts.SingleAsync(item => item.OrderId == order.Id);
        Assert.Equal("Succeeded", payment.Status);
        Assert.Single(await db.CardTransactions.Where(item => item.PaymentId == payment.Id).ToArrayAsync());
        Assert.Single(await db.PaymentReceipts.Where(item => item.PaymentId == payment.Id).ToArrayAsync());
        Assert.Single(await db.SubscriptionEvents.Where(item => item.RelatedPaymentId == payment.Id && item.EventType == "Renewed").ToArrayAsync());
        Assert.Single(await db.Notifications.Where(item => item.UserId == state.User.Id).ToArrayAsync());
        await Run(end.AddMinutes(1)); db.ChangeTracker.Clear();
        Assert.Equal(80.02m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.True((await db.BillingPreferences.SingleAsync(item => item.UserId == state.User.Id)).AutoRenew);
    }

    [Fact]
    public async Task ScheduledAnnualPriceAppliesOnlyToTheNewPeriod()
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var state = await Setup(browser);
        var selected = await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Put, "/api/v1/me/subscription/scheduled-plan",
            new { subscriptionId = state.Subscription.Id, planPriceId = BillingCatalogSeed.Annual, idempotencyKey = Guid.NewGuid() }, state.Token));
        Assert.Equal("Monthly", selected.Period);
        var end = state.Subscription.CurrentPeriodEnd!.Value; await Run(end);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var current = await db.Subscriptions.SingleAsync(item => item.Id == state.Subscription.Id);
        Assert.Equal(BillingCatalogSeed.Annual, current.CurrentPlanPriceId); Assert.Null(current.ScheduledPlanPriceId);
        Assert.Equal(end.AddYears(1), current.CurrentPeriodEnd);
        Assert.Equal(10.02m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.Equal(9.99m, (await db.BillingOrders.SingleAsync(item => item.SubscriptionId == current.Id && item.OrderType == "Purchase")).AmountSnapshot);
    }

    [Theory]
    [InlineData("CardBlocked")]
    [InlineData("InsufficientBalance")]
    [InlineData("CurrencyMismatch")]
    public async Task DeclinesExpireWithoutDebitAndNeverRetryAfterTheCardIsFixed(string failure)
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        if (failure == "CardBlocked") await db.TestCards.Where(item => item.Id == state.CardId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Blocked"));
        if (failure == "InsufficientBalance") await db.TestCards.Where(item => item.Id == state.CardId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Balance, 0m));
        if (failure == "CurrencyMismatch")
        {
            var foreignCurrency = await Read<CardChange>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards", new { nickname = "USD", currency = "USD", idempotencyKey = Guid.NewGuid() }, state.Token));
            await db.BillingPreferences.Where(item => item.UserId == state.User.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.DefaultTestCardId, foreignCurrency.Card.Id));
        }
        var before = (await db.TestCards.AsNoTracking().SingleAsync(item => item.Id == state.CardId)).Balance;
        var end = state.Subscription.CurrentPeriodEnd!.Value; await Run(end);
        var job = await db.BillingJobExecutions.SingleAsync(item => item.SubscriptionId == state.Subscription.Id);
        Assert.Equal("Failed", job.Status); Assert.Equal(failure, job.FailureCode); Assert.Null(job.NextAttemptAt);
        Assert.Equal("Expired", (await db.Subscriptions.SingleAsync(item => item.Id == state.Subscription.Id)).Status);
        Assert.Equal(state.Subscription.CurrentPeriodEnd, (await db.Subscriptions.SingleAsync(item => item.Id == state.Subscription.Id)).CurrentPeriodEnd);
        var notice = await db.Notifications.SingleAsync(item => item.UserId == state.User.Id);
        Assert.Equal("SubscriptionEnded", notice.Type); Assert.Contains("No funds were charged", notice.Body); Assert.Null(notice.ReadAt);
        var order = await db.BillingOrders.SingleAsync(item => item.SubscriptionId == state.Subscription.Id && item.OrderType == "Renewal");
        var payment = await db.PaymentAttempts.SingleAsync(item => item.OrderId == order.Id);
        Assert.Equal("Failed", payment.Status); Assert.False(await db.CardTransactions.AnyAsync(item => item.PaymentId == payment.Id));
        Assert.False(await db.PaymentReceipts.AnyAsync(item => item.PaymentId == payment.Id));
        await db.TestCards.Where(item => item.Id == state.CardId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Active"));
        await Run(end.AddMinutes(1)); db.ChangeTracker.Clear();
        Assert.Equal(before, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.Single(await db.BillingJobExecutions.Where(item => item.SubscriptionId == state.Subscription.Id).ToArrayAsync());
        Assert.False((await db.BillingPreferences.SingleAsync(item => item.UserId == state.User.Id)).AutoRenew);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("AutoRenewOff")]
    [InlineData("ConsentRequired")]
    [InlineData("MissedWindow")]
    public async Task UnapprovedCancelledAndMissedRenewalsNeverCreateAPayment(string reason)
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System));
        var state = await Setup(browser, reason != "ConsentRequired");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        if (reason == "Cancelled") (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/subscription/cancel", new { subscriptionId = state.Subscription.Id, idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        if (reason == "AutoRenewOff") await db.BillingPreferences.Where(item => item.UserId == state.User.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.AutoRenew, false).SetProperty(item => item.RenewalConsentAt, (DateTimeOffset?)null).SetProperty(item => item.RenewalConsentSubscriptionId, (Guid?)null));
        if (reason == "ConsentRequired")
        {
            db.BillingPreferences.Add(new BillingPreference { UserId = state.User.Id, DefaultTestCardId = state.CardId, AutoRenew = true });
            await db.SaveChangesAsync();
        }
        var end = state.Subscription.CurrentPeriodEnd!.Value;
        await Run(reason == "MissedWindow" ? end.AddMinutes(16) : end);
        var job = await db.BillingJobExecutions.SingleAsync(item => item.SubscriptionId == state.Subscription.Id);
        Assert.Equal("Skipped", job.Status); Assert.Equal(reason, job.FailureCode);
        Assert.False(await db.BillingOrders.AnyAsync(item => item.SubscriptionId == state.Subscription.Id && item.OrderType == "Renewal"));
        Assert.Equal(90.01m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateOptOutWorksBeforeThePendingWorkerAndOldSavedPreferencesNeedFreshConsent(bool cancel)
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        Assert.False(state.Subscription.RenewalProcessingAvailable); Assert.True(state.Subscription.RenewalConsentRequired);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Null((await db.BillingPreferences.SingleAsync(item => item.UserId == state.User.Id)).RenewalConsentAt);
        var end = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.Subscriptions.Where(item => item.Id == state.Subscription.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.CurrentPeriodStart, end.AddMonths(-1)).SetProperty(item => item.CurrentPeriodEnd, end));
        var free = await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: state.Token));
        Assert.Null(free.Id); Assert.Equal(state.Subscription.Id, free.RenewalTargetSubscriptionId);
        var stopped = cancel
            ? await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/subscription/cancel", new { subscriptionId = state.Subscription.Id, idempotencyKey = Guid.NewGuid() }, state.Token))
            : await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Put, "/api/v1/me/subscription/renewal",
                new { subscriptionId = state.Subscription.Id, autoRenew = false, defaultTestCardId = state.CardId, idempotencyKey = Guid.NewGuid() }, state.Token));
        Assert.False(stopped.AutoRenew);
        await Run(DateTimeOffset.UtcNow);
        Assert.Equal(cancel ? "Cancelled" : "AutoRenewOff", (await db.BillingJobExecutions.SingleAsync(item => item.SubscriptionId == state.Subscription.Id)).FailureCode);
    }

    [Fact]
    public async Task ANotificationFailureRollsBackTheDebitPeriodAndJobAndRecoveryIsSafe()
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fail_renewal_fixture() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected notification failure'; END; $$; CREATE TRIGGER fail_renewal_fixture BEFORE INSERT ON \"Notifications\" FOR EACH ROW EXECUTE FUNCTION fail_renewal_fixture();");
        var end = state.Subscription.CurrentPeriodEnd!.Value;
        try
        {
            await Run(end);
            Assert.False(await db.BillingJobExecutions.AnyAsync(item => item.SubscriptionId == state.Subscription.Id));
            Assert.Equal(end, (await db.Subscriptions.SingleAsync(item => item.Id == state.Subscription.Id)).CurrentPeriodEnd);
            Assert.Equal(90.01m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
            Assert.False(await db.BillingOrders.AnyAsync(item => item.SubscriptionId == state.Subscription.Id && item.OrderType == "Renewal"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_renewal_fixture ON \"Notifications\"; DROP FUNCTION fail_renewal_fixture();"); }
        await Run(end); db.ChangeTracker.Clear();
        Assert.Equal(80.02m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
    }

    [Fact]
    public async Task DatabaseRejectsSuccessfulJobsWithoutPaymentAndDisabledProcessingDoesNothing()
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var state = await Setup(browser);
        Assert.Equal(0, await Run(state.Subscription.CurrentPeriodEnd!.Value, false));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        db.BillingJobExecutions.Add(new BillingJobExecution { SubscriptionId = state.Subscription.Id!.Value, PeriodKey = "orphan", Status = "Succeeded", LastAttemptAt = DateTimeOffset.UtcNow });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task APriceRetiredBeforeRenewalDoesNotSilentlyChargeAnotherPrice()
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var end = state.Subscription.CurrentPeriodEnd!.Value;
        await db.PlanPrices.Where(item => item.Id == BillingCatalogSeed.Monthly).ExecuteUpdateAsync(update => update.SetProperty(item => item.ValidTo, end));
        try
        {
            await Run(end);
            Assert.Equal("PriceUnavailable", (await db.BillingJobExecutions.SingleAsync(item => item.SubscriptionId == state.Subscription.Id)).FailureCode);
            Assert.Equal(90.01m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        }
        finally { await db.PlanPrices.Where(item => item.Id == BillingCatalogSeed.Monthly).ExecuteUpdateAsync(update => update.SetProperty(item => item.ValidTo, (DateTimeOffset?)null)); }
    }

    [Fact]
    public async Task UnconfirmedAccountsAreNotChargedAndCompletedJobsCannotBeRewrittenOrDuplicated()
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var end = state.Subscription.CurrentPeriodEnd!.Value;
        await db.Users.Where(item => item.Id == state.User.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, false));
        await Run(end);
        Assert.False(await db.BillingJobExecutions.AnyAsync(item => item.SubscriptionId == state.Subscription.Id));
        Assert.Equal(90.01m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        await db.Users.Where(item => item.Id == state.User.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, true));
        await Run(end);
        var job = await db.BillingJobExecutions.AsNoTracking().SingleAsync(item => item.SubscriptionId == state.Subscription.Id);
        await Assert.ThrowsAsync<PostgresException>(() => db.BillingJobExecutions.Where(item => item.Id == job.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Failed")));
        db.BillingJobExecutions.Add(new BillingJobExecution { SubscriptionId = job.SubscriptionId, PeriodKey = job.PeriodKey, Status = "Skipped", FailureCode = "Duplicate", LastAttemptAt = end });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task ManualPurchaseAfterAFailedRenewalStartsANewPeriodWithoutRetryingOldCharges()
    {
        using var browser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var end = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.Subscriptions.Where(item => item.Id == state.Subscription.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.CurrentPeriodStart, end.AddMonths(-1)).SetProperty(item => item.CurrentPeriodEnd, end));
        await db.BillingPreferences.Where(item => item.UserId == state.User.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RenewalConsentAt, end.AddMinutes(-1)));
        await db.TestCards.Where(item => item.Id == state.CardId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Blocked"));
        await Run(DateTimeOffset.UtcNow);
        Assert.Equal("FREE", (await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: state.Token))).PlanCode);
        await db.TestCards.Where(item => item.Id == state.CardId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Active"));
        var before = DateTimeOffset.UtcNow;
        var order = await Read<BillingOrderView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/billing-orders", new { planPriceId = BillingCatalogSeed.Monthly, idempotencyKey = Guid.NewGuid() }, state.Token));
        var payment = await Read<PaymentView>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/billing-orders/{order.Id}/payments", new { testCardId = state.CardId, idempotencyKey = Guid.NewGuid() }, state.Token));
        Assert.Equal("Succeeded", payment.Status); Assert.NotEqual(state.Subscription.Id, payment.SubscriptionId);
        var current = await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: state.Token));
        Assert.Equal("PREMIUM", current.PlanCode); Assert.False(current.AutoRenew);
        Assert.True(current.CurrentPeriodStart >= before.AddMilliseconds(-1));
        Assert.Equal("Failed", (await db.BillingJobExecutions.SingleAsync(item => item.SubscriptionId == state.Subscription.Id)).Status);
        Assert.Equal(80.02m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        await Run(DateTimeOffset.UtcNow);
        Assert.Single(await db.BillingOrders.Where(item => item.SubscriptionId == state.Subscription.Id && item.OrderType == "Renewal").ToArrayAsync());
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/subscription/renewal", new { subscriptionId = current.Id, autoRenew = true, defaultTestCardId = state.CardId, idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/subscription/cancel", new { subscriptionId = state.Subscription.Id, idempotencyKey = Guid.NewGuid() }, state.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/subscription/renewal", new { subscriptionId = state.Subscription.Id, autoRenew = false, defaultTestCardId = state.CardId, idempotencyKey = Guid.NewGuid() }, state.Token)).StatusCode);
        Assert.True((await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: state.Token))).AutoRenew);
    }
}
