using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Tests.Billing;

public sealed class SubscriptionManagementTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private sealed class ExpiryClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    [Fact]
    public async Task CancelledSubscriptionExpiresToFreeAndKeepsCompletedLessonProgress()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await Setup(browser);
        var lesson = await LearningTestContent.CreateAsync(fixture, state.User.Id, count: 1);
        var enrollment = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments",
            new { lesson.CourseId }, state.Token);
        enrollment.EnsureSuccessStatusCode();
        var session = await Read<SessionView>(await browser.SendAsync(HttpMethod.Post,
            $"/api/v1/lessons/{lesson.LessonId}/sessions", bearer: state.Token));
        var sessionRoute = $"/api/v1/me/learning-sessions/{session.Id}";
        var answer = await browser.SendAsync(HttpMethod.Post, sessionRoute + "/answers", new
        {
            sessionExerciseId = session.CurrentExercise!.Id, answerText = "Hola",
            selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid()
        }, state.Token);
        answer.EnsureSuccessStatusCode();
        var completed = await Read<SessionView>(await browser.SendAsync(HttpMethod.Post,
            sessionRoute + "/complete", bearer: state.Token));
        Assert.True(completed.Passed);
        Assert.True(completed.AwardedXp > 0);

        (await Cancel(browser, state, Guid.NewGuid())).EnsureSuccessStatusCode();
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var recorder = scope.ServiceProvider.GetRequiredService<SubscriptionPaymentRecorder>();
        var processor = new SubscriptionRenewalProcessor(database, recorder,
            new ExpiryClock(state.Subscription.CurrentPeriodEnd!.Value),
            Options.Create(new BillingRenewalOptions { Enabled = true }),
            NullLogger<SubscriptionRenewalProcessor>.Instance);
        Assert.True(await processor.ProcessDueAsync(default) > 0);

        var entitlements = await Read<EntitlementView>(await browser.SendAsync(HttpMethod.Get,
            "/api/v1/me/entitlements", bearer: state.Token));
        Assert.Equal("FREE", entitlements.PlanCode);
        var subscription = await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get,
            "/api/v1/me/subscription", bearer: state.Token));
        Assert.Equal("FREE", subscription.PlanCode);
        Assert.Null(subscription.CurrentPeriodEnd);
        var progress = await Read<CourseProgress>(await browser.SendAsync(HttpMethod.Get,
            $"/api/v1/me/progress?courseId={lesson.CourseId}", bearer: state.Token));
        Assert.Equal("Completed", Assert.Single(progress.Lessons, row => row.Id == lesson.LessonId).Status);
        Assert.Equal(completed.AwardedXp, (await Read<SessionView>(await browser.SendAsync(
            HttpMethod.Get, sessionRoute, bearer: state.Token))).AwardedXp);
    }

    [Fact]
    public async Task PremiumStartsAtTheUtcStartAndEndsAtTheUtcEndBeforeTheWorkerRuns()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await Setup(browser);
        (await Cancel(browser, state, Guid.NewGuid())).EnsureSuccessStatusCode();
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<BillingRenewalOptions>>();
        var period = await database.Subscriptions.AsNoTracking()
            .SingleAsync(item => item.Id == state.Subscription.Id);

        var boundaries = new[]
        {
            (period.CurrentPeriodStart.AddMicroseconds(-1), false),
            (period.CurrentPeriodStart, true),
            (period.CurrentPeriodEnd.AddMicroseconds(-1), true),
            (period.CurrentPeriodEnd, false)
        };
        foreach (var (instant, active) in boundaries)
        {
            var queries = new SubscriptionQueries(database, new ExpiryClock(instant), options);
            var entitlements = await queries.EntitlementsAsync(state.User.Id, default);
            Assert.Equal(active ? "PREMIUM" : "FREE", entitlements.PlanCode);
            Assert.Equal(active, await queries.HasAsync(state.User.Id, "UnlimitedHearts", default));
        }
    }

    [Fact]
    public async Task SettingsQuotaDoesNotBlockCancellationOrTurningRenewalOff()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await Setup(browser);
        for (int index = 0; index < 20; index++) (await Schedule(browser, state, null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Schedule(browser, state, BillingCatalogSeed.Annual)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Cancel(browser, state, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Renewal(browser, state, false, null)).StatusCode);
    }
    private sealed record State(SecurityTestAccount User, string Token, Guid CardId, SubscriptionView Subscription);
    private async Task<State> Setup(SessionTestBrowser browser)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(browser);
        var card = await Read<CardChange>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards", new { nickname = "Renewal card", currency = "GEL", idempotencyKey = Guid.NewGuid() }, token));
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{card.Card.Id}/top-ups", new { amount = 100, idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
        var order = await Read<BillingOrderView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/billing-orders", new { planPriceId = BillingCatalogSeed.Monthly, idempotencyKey = Guid.NewGuid() }, token));
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/billing-orders/{order.Id}/payments", new { testCardId = card.Card.Id, idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
        var subscription = await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: token));
        return new(user, token, card.Card.Id, subscription);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>())!; }
    private static Task<HttpResponseMessage> Cancel(SessionTestBrowser browser, State state, Guid key, bool cancel = true, Guid? id = null) => browser.SendAsync(HttpMethod.Post,
        "/api/v1/me/subscription/" + (cancel ? "cancel" : "resume"), new { subscriptionId = id ?? state.Subscription.Id, idempotencyKey = key }, state.Token);
    private static Task<HttpResponseMessage> Renewal(SessionTestBrowser browser, State state, bool enabled, Guid? card, Guid? key = null) => browser.SendAsync(HttpMethod.Put,
        "/api/v1/me/subscription/renewal", new { subscriptionId = state.Subscription.Id, autoRenew = enabled, defaultTestCardId = card, idempotencyKey = key ?? Guid.NewGuid() }, state.Token);
    private static Task<HttpResponseMessage> Schedule(SessionTestBrowser browser, State state, Guid? price, Guid? key = null) => browser.SendAsync(HttpMethod.Put,
        "/api/v1/me/subscription/scheduled-plan", new { subscriptionId = state.Subscription.Id, planPriceId = price, idempotencyKey = key ?? Guid.NewGuid() }, state.Token);

    [Fact]
    public async Task CancelAndResumePreservePaidAccessDatesMoneyAndReplayEvidence()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser); Guid key = Guid.NewGuid();
        var canceled = await Read<SubscriptionView>(await Cancel(browser, state, key)); Assert.True(canceled.CancelAtPeriodEnd);
        Assert.Equal(state.Subscription.CurrentPeriodEnd, canceled.CurrentPeriodEnd); Assert.Equal("PREMIUM", canceled.PlanCode);
        var access = await Read<EntitlementView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/entitlements", bearer: state.Token)); Assert.Equal("PREMIUM", access.PlanCode);
        Assert.Equal(canceled, await Read<SubscriptionView>(await Cancel(browser, state, key)));
        var resumed = await Read<SubscriptionView>(await Cancel(browser, state, Guid.NewGuid(), false)); Assert.False(resumed.CancelAtPeriodEnd);
        // Historical replay must never cancel a newer state again.
        Assert.Equal(canceled, await Read<SubscriptionView>(await Cancel(browser, state, key)));
        Assert.False((await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: state.Token))).CancelAtPeriodEnd);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(90.01m, await db.TestCards.Where(item => item.Id == state.CardId).Select(item => item.Balance).SingleAsync());
        Assert.Equal(1, await db.SubscriptionEvents.CountAsync(item => item.SubscriptionId == state.Subscription.Id && item.EventType == "CancellationRequested"));
    }

    [Fact]
    public async Task RenewalPreferencesRequireOwnedActiveMatchingCardAndDoNotCharge()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        Assert.Equal(HttpStatusCode.Conflict, (await Renewal(browser, state, true, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Renewal(browser, state, true, Guid.NewGuid())).StatusCode);
        var saved = await Read<SubscriptionView>(await Renewal(browser, state, true, state.CardId)); Assert.True(saved.AutoRenew); Assert.Equal(state.CardId, saved.DefaultTestCardId);
        Assert.False(saved.RenewalProcessingAvailable);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{state.CardId}/block", new { idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Renewal(browser, state, true, state.CardId)).StatusCode);
        var disabled = await Read<SubscriptionView>(await Renewal(browser, state, false, state.CardId)); Assert.False(disabled.AutoRenew);
        var off = await Read<SubscriptionView>(await Renewal(browser, state, false, null)); Assert.False(off.AutoRenew); Assert.Null(off.DefaultTestCardId);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await db.PaymentAttempts.CountAsync(item => item.UserId == state.User.Id));
        Assert.Equal(90.01m, await db.TestCards.Where(item => item.Id == state.CardId).Select(item => item.Balance).SingleAsync());
    }

    [Fact]
    public async Task ScheduledAnnualChangeDoesNotAlterCurrentMonthlyPeriodAndCanBeCleared()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser); Guid key = Guid.NewGuid();
        var scheduled = await Read<SubscriptionView>(await Schedule(browser, state, BillingCatalogSeed.Annual, key));
        Assert.Equal("Monthly", scheduled.Period); Assert.Equal(state.Subscription.CurrentPeriodEnd, scheduled.CurrentPeriodEnd);
        Assert.Equal(BillingCatalogSeed.Annual, scheduled.ScheduledPlanPriceId); Assert.Equal(79.99m, scheduled.ScheduledPlanPrice!.Amount);
        Assert.Equal(scheduled, await Read<SubscriptionView>(await Schedule(browser, state, BillingCatalogSeed.Annual, key)));
        Assert.Equal(HttpStatusCode.Conflict, (await Schedule(browser, state, null, key)).StatusCode);
        var cleared = await Read<SubscriptionView>(await Schedule(browser, state, null)); Assert.Null(cleared.ScheduledPlanPriceId); Assert.Null(cleared.ScheduledPlanPrice);
    }

    [Fact]
    public async Task ForeignTargetsAreDeniedAndEndedSubscriptionsCanOnlyStopRenewal()
    {
        using var owner = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(owner);
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient()); var other = await Setup(outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await Cancel(outsider, other, Guid.NewGuid(), id: state.Subscription.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Renewal(outsider, other, true, state.CardId)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Subscriptions.Where(item => item.Id == state.Subscription.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.CurrentPeriodStart, DateTimeOffset.UtcNow.AddMonths(-2)).SetProperty(item => item.CurrentPeriodEnd, DateTimeOffset.UtcNow.AddMonths(-1)));
        Assert.Equal(HttpStatusCode.OK, (await Cancel(owner, state, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Cancel(owner, state, Guid.NewGuid(), false)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Schedule(owner, state, BillingCatalogSeed.Annual)).StatusCode);
    }

    [Fact]
    public async Task CancelPreventsEnablingRenewalUntilExplicitResume()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        await Read<SubscriptionView>(await Renewal(browser, state, true, state.CardId));
        var canceled = await Read<SubscriptionView>(await Cancel(browser, state, Guid.NewGuid())); Assert.True(canceled.AutoRenew); Assert.True(canceled.CancelAtPeriodEnd);
        Assert.Equal(HttpStatusCode.Conflict, (await Renewal(browser, state, true, state.CardId)).StatusCode);
        var resumed = await Read<SubscriptionView>(await Cancel(browser, state, Guid.NewGuid(), false)); Assert.True(resumed.AutoRenew); Assert.False(resumed.CancelAtPeriodEnd);
    }

    [Fact]
    public async Task ANewManualPurchaseDoesNotInheritAnOldRenewalConsent()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        await Read<SubscriptionView>(await Renewal(browser, state, true, state.CardId));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Subscriptions.Where(item => item.Id == state.Subscription.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.CurrentPeriodStart, DateTimeOffset.UtcNow.AddMonths(-2)).SetProperty(item => item.CurrentPeriodEnd, DateTimeOffset.UtcNow.AddMonths(-1)));
        var order = await Read<BillingOrderView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/billing-orders", new { planPriceId = BillingCatalogSeed.Monthly, idempotencyKey = Guid.NewGuid() }, state.Token));
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/billing-orders/{order.Id}/payments", new { testCardId = state.CardId, idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        var current = await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: state.Token));
        Assert.NotEqual(state.Subscription.Id, current.Id); Assert.False(current.AutoRenew); Assert.Equal(state.CardId, current.DefaultTestCardId);
    }

    [Fact]
    public async Task ConcurrentDuplicateCancellationWritesOneEventAndOperation()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser); Guid key = Guid.NewGuid();
        var responses = await Task.WhenAll(Cancel(browser, state, key), Cancel(browser, state, key));
        Assert.Equal(await Read<SubscriptionView>(responses[0]), await Read<SubscriptionView>(responses[1]));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await db.BillingOperations.CountAsync(item => item.UserId == state.User.Id && item.Operation == "Subscription.Cancel"));
        Assert.Equal(1, await db.SubscriptionEvents.CountAsync(item => item.SubscriptionId == state.Subscription.Id && item.EventType == "CancellationRequested"));
    }

    [Fact]
    public async Task ReplayInsertFailureRollsBackPreferenceEventAndAudit()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fail_management_fixture() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Deliberate isolated management failure.'; END; $body$;
            CREATE TRIGGER fail_management_fixture BEFORE INSERT ON "BillingOperations"
                FOR EACH ROW WHEN (NEW."Operation" = 'Subscription.Cancel') EXECUTE FUNCTION fail_management_fixture();
            """);
        Guid key = Guid.NewGuid();
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await Cancel(browser, state, key)).StatusCode);
            Assert.False(await db.Subscriptions.Where(item => item.Id == state.Subscription.Id).Select(item => item.CancelAtPeriodEnd).SingleAsync());
            Assert.False(await db.SubscriptionEvents.AnyAsync(item => item.SubscriptionId == state.Subscription.Id && item.EventType == "CancellationRequested"));
            Assert.False(await db.AuditLogs.AnyAsync(item => item.ActorId == state.User.Id && item.Action == "Subscription.Cancel"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_management_fixture ON \"BillingOperations\"; DROP FUNCTION fail_management_fixture();"); }
        Assert.True((await Read<SubscriptionView>(await Cancel(browser, state, key))).CancelAtPeriodEnd);
    }
}
