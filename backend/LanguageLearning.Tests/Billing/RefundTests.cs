using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Tests.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Microsoft.AspNetCore.Mvc.DataAnnotations;

namespace LanguageLearning.Tests.Billing;

public sealed partial class RefundTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task ExistingRefundReasonsRetainReplayBehaviorAfterThePrivacyMigration()
    {
        var isolated = new RegistrationFixture();
        await isolated.InitializeAsync();
        try
        {
            var cases = new RefundTests(isolated);
            using var buyer = new SessionTestBrowser(isolated.CreateIsolatedClient());
            using var staff = new SessionTestBrowser(isolated.CreateIsolatedClient());
            var customer = await cases.Setup(buyer);
            var actor = await cases.Actor(staff);
            Guid key = Guid.NewGuid();
            var initial = await Read<RefundView>(await Post(staff, actor.Token, customer.Payment.Id, 1m, key));
            await using (var scope = isolated.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
                await db.Database.MigrateAsync("20261117141202_AddLeaderboardAvatarConsent");
                await db.Database.MigrateAsync();
                var restored = await db.Refunds.AsNoTracking().SingleAsync(item => item.Id == initial.Id);
                Assert.Equal(initial.Reason, restored.Reason);
                Assert.Equal(64, restored.ReasonFingerprint.Length);
            }
            var replay = await Read<RefundView>(await Post(staff, actor.Token, customer.Payment.Id, 1m, key));
            Assert.Equal(initial, replay);
            using var keeper = new SessionTestBrowser(isolated.CreateIsolatedClient());
            _ = await cases.Actor(keeper);
            using var deletion = await staff.SendAsync(HttpMethod.Delete, "/api/v1/me", new
            {
                currentPassword = SecurityTestAccount.Password, confirmation = "DELETE",
                recoveryCode = actor.User.RecoveryCodes[1]
            }, actor.Token, csrf: true);
            Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
            await using var retainedScope = isolated.Services.CreateAsyncScope();
            var retained = retainedScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var rollback = await Assert.ThrowsAsync<PostgresException>(() => retained.Database
                .MigrateAsync("20261118074347_AllowDeletedAccountRefunds"));
            Assert.Contains("Retain deleted-staff refund privacy support", rollback.MessageText);
            Assert.Contains("20261120083352_AllowDepartingStaffNoteErasure",
                await retained.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await isolated.DisposeAsync();
        }
    }

    [Fact]
    public async Task DeletionErasesPersonalRefundNotesWhileMoneyAndReplayEvidenceRemainProtected()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var customer = await Setup(buyer);
        var actor = await Actor(staff);
        Guid requestKey = Guid.NewGuid();
        string reason = "Request from " + customer.User.Email;
        var request = new
        {
            amount = 1m, reason,
            currentPassword = SecurityTestAccount.Password, idempotencyKey = requestKey
        };
        (await staff.SendAsync(HttpMethod.Post, $"/api/v1/admin/payments/{customer.Payment.Id}/refunds",
            request, actor.Token)).EnsureSuccessStatusCode();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var denied = await Assert.ThrowsAsync<PostgresException>(() => db.Refunds
                .Where(item => item.PaymentId == customer.Payment.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Reason, Refund.ErasedReason)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, denied.SqlState);
        }
        var deletion = await buyer.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password, confirmation = "DELETE",
            recoveryCode = customer.User.RecoveryCodes[1]
        }, customer.Token, csrf: true);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var refund = await db.Refunds.SingleAsync(item => item.PaymentId == customer.Payment.Id);
            Assert.Equal(1m, refund.Amount);
            Assert.Equal(Refund.ErasedReason, refund.Reason);
            Assert.Equal(64, refund.ReasonFingerprint.Length);
            Assert.DoesNotContain(customer.User.Email, string.Join(" ", await db.AuditLogs
                .Where(item => item.EntityType == "Refund" && item.EntityId == refund.Id.ToString())
                .Select(item => item.Reason).ToArrayAsync()));
            var denied = await Assert.ThrowsAsync<PostgresException>(() => db.Refunds
                .Where(item => item.Id == refund.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Amount, 2m)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, denied.SqlState);
        }
        using var replay = await staff.SendAsync(HttpMethod.Post,
            $"/api/v1/admin/payments/{customer.Payment.Id}/refunds", request, actor.Token);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(Refund.ErasedReason, (await replay.Content.ReadFromJsonAsync<RefundView>())!.Reason);
        using var laterRefund = await staff.SendAsync(HttpMethod.Post,
            $"/api/v1/admin/payments/{customer.Payment.Id}/refunds", new
            {
                amount = 1m, reason = "Follow-up for " + customer.User.Email,
                currentPassword = SecurityTestAccount.Password, idempotencyKey = Guid.NewGuid(),
            }, actor.Token);
        Assert.Equal(HttpStatusCode.OK, laterRefund.StatusCode);
        Assert.Equal(Refund.ErasedReason, (await laterRefund.Content.ReadFromJsonAsync<RefundView>())!.Reason);
    }

    [Fact]
    public async Task PersonalExportScopesReceiptsRefundsAndWalletRecordsToTheBuyer()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var customer = await Setup(buyer);
        var other = await Setup(outsider);
        var actor = await Actor(staff);
        var refund = await Read<RefundView>(await Post(staff, actor.Token, customer.Payment.Id, 2m));
        await Read<RefundView>(await Post(staff, actor.Token, other.Payment.Id, 3m));

        var response = await buyer.SendAsync(HttpMethod.Get, "/api/v1/me/export?userId=" + other.User.Id, bearer: customer.Token);
        var data = await Read<System.Text.Json.JsonElement>(response);
        var billing = data.GetProperty("simulatedBilling");
        Assert.Equal("simulated", billing.GetProperty("mode").GetString());
        Assert.Equal(customer.CardId, Assert.Single(billing.GetProperty("testCards").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.All(billing.GetProperty("walletTransactions").EnumerateArray(), item =>
            Assert.Equal(customer.CardId, item.GetProperty("testCardId").GetGuid()));
        Assert.Equal(customer.Payment.OrderId, Assert.Single(billing.GetProperty("orders").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(customer.Payment.Id, Assert.Single(billing.GetProperty("payments").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(customer.Payment.Id, Assert.Single(billing.GetProperty("receipts").EnumerateArray()).GetProperty("paymentId").GetGuid());
        Assert.Equal(refund.Id, Assert.Single(billing.GetProperty("refunds").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(customer.Payment.SubscriptionId, Assert.Single(billing.GetProperty("subscriptions").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.DoesNotContain(other.Payment.Id.ToString(), billing.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotencyKey", billing.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdBy", billing.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var deleted = await buyer.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password, confirmation = "DELETE",
            recoveryCode = customer.User.RecoveryCodes[1]
        }, customer.Token, csrf: true);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var deletedScope = fixture.Services.CreateScope();
        var db = deletedScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.True(await db.PaymentAttempts.AnyAsync(item => item.Id == customer.Payment.Id));
        Assert.True(await db.Refunds.AnyAsync(item => item.Id == refund.Id));
        var erasedCard = await db.TestCards.SingleAsync(item => item.Id == customer.CardId);
        Assert.Equal("Deleted account", erasedCard.Nickname);
        Assert.Equal("Blocked", erasedCard.Status);
        Assert.Equal("Expired", (await db.Subscriptions.SingleAsync(item => item.UserId == customer.User.Id)).Status);
        Assert.False(await db.BillingPreferences.AnyAsync(item => item.UserId == customer.User.Id));
        Assert.False(await db.BillingOperations.AnyAsync(item => item.UserId == customer.User.Id));
        Assert.True(await db.BillingOperations.AnyAsync(item => item.UserId == other.User.Id));
    }

    private sealed record Customer(SecurityTestAccount User, string Token, Guid CardId, PaymentView Payment);
    private async Task<(SecurityTestAccount User, string Token)> Actor(SessionTestBrowser browser, string role = RoleNames.SuperAdmin)
    {
        var actor = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(actor.Id.ToString()))!, role)).Succeeded);
        return (actor, await actor.LoginAsync(browser));
    }
    private async Task<Customer> Setup(SessionTestBrowser browser)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(browser);
        var card = await Read<CardChange>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards", new { nickname = "Refund card", currency = "GEL", idempotencyKey = Guid.NewGuid() }, token));
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{card.Card.Id}/top-ups", new { amount = 100, idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
        var paid = await Buy(browser, token, card.Card.Id);
        return new(user, token, card.Card.Id, paid);
    }
    private static async Task<PaymentView> Buy(SessionTestBrowser browser, string token, Guid card)
    {
        var order = await Read<BillingOrderView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/billing-orders", new { planPriceId = BillingCatalogSeed.Monthly, idempotencyKey = Guid.NewGuid() }, token));
        return await Read<PaymentView>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/billing-orders/{order.Id}/payments", new { testCardId = card, idempotencyKey = Guid.NewGuid() }, token));
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static Task<HttpResponseMessage> Post(SessionTestBrowser browser, string token, Guid payment, decimal amount, Guid? key = null, string password = SecurityTestAccount.Password) =>
        browser.SendAsync(HttpMethod.Post, $"/api/v1/admin/payments/{payment}/refunds", new { amount, reason = "Customer requested a test refund", currentPassword = password, idempotencyKey = key ?? Guid.NewGuid() }, token);
    private static Task<HttpResponseMessage> Renewal(SessionTestBrowser browser, Customer customer) => browser.SendAsync(HttpMethod.Put, "/api/v1/me/subscription/renewal",
        new { subscriptionId = customer.Payment.SubscriptionId, autoRenew = true, defaultTestCardId = customer.CardId, idempotencyKey = Guid.NewGuid() }, customer.Token);

    [Fact]
    public async Task FullCurrentRefundCreditsOriginalBlockedCardRevokesOnlyAccessAndReplaysOnce()
    {
        using var customerBrowser = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var customer = await Setup(customerBrowser);
        (await Renewal(customerBrowser, customer)).EnsureSuccessStatusCode();
        (await customerBrowser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{customer.CardId}/block", new { idempotencyKey = Guid.NewGuid() }, customer.Token)).EnsureSuccessStatusCode();
        using var actorBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var actor = await Actor(actorBrowser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"LearnerHearts\" (\"UserId\",\"Count\",\"RefillAt\") VALUES ({customer.User.Id},3,NULL) ON CONFLICT (\"UserId\") DO UPDATE SET \"Count\" = 3,\"RefillAt\" = NULL");
        Guid key = Guid.NewGuid(); var refund = await Read<RefundView>(await Post(actorBrowser, actor.Token, customer.Payment.Id, 9.99m, key));
        Assert.True(refund.RevokedPremium); Assert.Equal(refund, await Read<RefundView>(await Post(actorBrowser, actor.Token, customer.Payment.Id, 9.99m, key)));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(actorBrowser, actor.Token, customer.Payment.Id, 9.98m, key)).StatusCode);
        Assert.Equal(refund, await Read<RefundView>(await actorBrowser.SendAsync(HttpMethod.Get, $"/api/v1/admin/refunds/by-request/{key}", bearer: actor.Token)));
        var card = await db.TestCards.SingleAsync(item => item.Id == customer.CardId); Assert.Equal(100m, card.Balance); Assert.Equal("Blocked", card.Status);
        Assert.Equal("Refunded", (await db.Subscriptions.SingleAsync(item => item.Id == customer.Payment.SubscriptionId)).Status);
        Assert.False((await db.BillingPreferences.SingleAsync(item => item.UserId == customer.User.Id)).AutoRenew);
        Assert.Equal("Succeeded", (await db.PaymentAttempts.SingleAsync(item => item.Id == customer.Payment.Id)).Status);
        Assert.Equal(3, (await db.LearnerHearts.SingleAsync(item => item.UserId == customer.User.Id)).Count);
        Assert.Single(await db.CardTransactions.Where(item => item.RefundId == refund.Id).ToArrayAsync());
        Assert.Single(await db.SubscriptionEvents.Where(item => item.RefundId == refund.Id).ToArrayAsync());
        Assert.Single(await db.Notifications.Where(item => item.UserId == customer.User.Id && item.Type == "PaymentRefunded").ToArrayAsync());
        var view = await Read<EntitlementView>(await customerBrowser.SendAsync(HttpMethod.Get, "/api/v1/me/entitlements", bearer: customer.Token)); Assert.Equal("FREE", view.PlanCode);
        var history = await Read<BillingActivityPage>(await customerBrowser.SendAsync(HttpMethod.Get, "/api/v1/me/transactions?type=Refund", bearer: customer.Token));
        var credit = Assert.Single(history.Items); Assert.Equal(refund.Id, credit.RefundId); Assert.Equal(customer.Payment.Id, credit.PaymentId); Assert.Equal(customer.Payment.OrderId, credit.OrderId);
        var cardHistory = await Read<BillingActivityPage>(await customerBrowser.SendAsync(HttpMethod.Get,
            $"/api/v1/me/test-cards/{customer.CardId}/transactions?type=Refund", bearer: customer.Token));
        Assert.Equal(credit, Assert.Single(cardHistory.Items));
        await Assert.ThrowsAsync<PostgresException>(() => db.Refunds.Where(item => item.Id == refund.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Amount, 1m)));
    }

    [Fact]
    public async Task PartialRefundsKeepPremiumUntilTheCumulativeFullAmountAndCannotExceedIt()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var customer = await Setup(buyer); (await Renewal(buyer, customer)).EnsureSuccessStatusCode();
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient()); var actor = await Actor(staff);
        var partial = await Read<RefundView>(await Post(staff, actor.Token, customer.Payment.Id, 4m)); Assert.False(partial.RevokedPremium);
        Assert.Equal("PREMIUM", (await Read<SubscriptionView>(await buyer.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: customer.Token))).PlanCode);
        Assert.True((await Read<SubscriptionView>(await buyer.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: customer.Token))).AutoRenew);
        Assert.True((await Read<RefundView>(await Post(staff, actor.Token, customer.Payment.Id, 5.99m))).RevokedPremium);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(staff, actor.Token, customer.Payment.Id, 0.01m)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(9.99m, await db.Refunds.Where(item => item.PaymentId == customer.Payment.Id).SumAsync(item => item.Amount));
        Assert.Equal(100m, (await db.TestCards.SingleAsync(item => item.Id == customer.CardId)).Balance);
    }

    [Fact]
    public async Task ConcurrentDifferentKeysCannotOverRefundTheSamePayment()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient()); var customer = await Setup(buyer);
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient()); var actor = await Actor(staff);
        var responses = await Task.WhenAll(Post(staff, actor.Token, customer.Payment.Id, 6m), Post(staff, actor.Token, customer.Payment.Id, 6m));
        Assert.Single(responses, item => item.IsSuccessStatusCode); Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Conflict);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Single(await db.Refunds.Where(item => item.PaymentId == customer.Payment.Id).ToArrayAsync());
        Assert.Equal(96.01m, (await db.TestCards.SingleAsync(item => item.Id == customer.CardId)).Balance);
    }

    [Fact]
    public async Task CurrentPasswordRecentMfaAndNoSelfApprovalAreRequired()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient()); var customer = await Setup(buyer);
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient()); var actor = await Actor(staff);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(staff, actor.Token, customer.Payment.Id, 1m, password: "Wrong password 42!")).StatusCode);
        await actor.User.ExpireFamilyAuthenticationAsync(fixture);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(staff, actor.Token, customer.Payment.Id, 1m)).StatusCode);
        using var self = new SessionTestBrowser(fixture.CreateIsolatedClient()); var privilegedCustomer = await Setup(self);
        using var scope = fixture.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(privilegedCustomer.User.Id.ToString()))!; Assert.True((await users.AddToRoleAsync(user, RoleNames.SuperAdmin)).Succeeded);
        string token = await privilegedCustomer.User.LoginAsync(self, 1);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(self, token, privilegedCustomer.Payment.Id, 1m)).StatusCode);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().Refunds.Where(item => item.PaymentId == customer.Payment.Id || item.PaymentId == privilegedCustomer.Payment.Id).ToArrayAsync());
    }

    [Fact]
    public async Task NoticeFailureRollsBackRefundCreditRevocationAndEvidence()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient()); var customer = await Setup(buyer);
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient()); var actor = await Actor(staff);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fail_refund_fixture() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Type\" = 'PaymentRefunded' THEN RAISE EXCEPTION 'injected refund notice failure'; END IF; RETURN NEW; END; $$; CREATE TRIGGER fail_refund_fixture BEFORE INSERT ON \"Notifications\" FOR EACH ROW EXECUTE FUNCTION fail_refund_fixture();");
        Guid key = Guid.NewGuid();
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await Post(staff, actor.Token, customer.Payment.Id, 9.99m, key)).StatusCode);
            Assert.False(await db.Refunds.AnyAsync(item => item.PaymentId == customer.Payment.Id));
            Assert.Equal(90.01m, (await db.TestCards.SingleAsync(item => item.Id == customer.CardId)).Balance);
            Assert.Equal("Active", (await db.Subscriptions.SingleAsync(item => item.Id == customer.Payment.SubscriptionId)).Status);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_refund_fixture ON \"Notifications\"; DROP FUNCTION fail_refund_fixture();"); }
        Assert.True((await Read<RefundView>(await Post(staff, actor.Token, customer.Payment.Id, 9.99m, key))).RevokedPremium);
    }

    [Fact]
    public async Task RefundCreditsCanRestoreFundsAboveTheTopUpCeilingEvenOnABlockedCard()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient()); var customer = await Setup(buyer);
        for (int index = 0; index < 9; index++) (await buyer.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{customer.CardId}/top-ups", new { amount = 1000, idempotencyKey = Guid.NewGuid() }, customer.Token)).EnsureSuccessStatusCode();
        (await buyer.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{customer.CardId}/top-ups", new { amount = 909.99m, idempotencyKey = Guid.NewGuid() }, customer.Token)).EnsureSuccessStatusCode();
        (await buyer.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{customer.CardId}/block", new { idempotencyKey = Guid.NewGuid() }, customer.Token)).EnsureSuccessStatusCode();
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient()); var actor = await Actor(staff);
        (await Post(staff, actor.Token, customer.Payment.Id, 9.99m)).EnsureSuccessStatusCode();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(10009.99m, (await db.TestCards.SingleAsync(item => item.Id == customer.CardId)).Balance);
        (await buyer.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{customer.CardId}/unblock", new { idempotencyKey = Guid.NewGuid() }, customer.Token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{customer.CardId}/top-ups", new { amount = 1, idempotencyKey = Guid.NewGuid() }, customer.Token)).StatusCode);
    }

    [Fact]
    public async Task RefundOfAnEarlierRenewedPeriodDoesNotRevokeTheCurrentPeriodOrConsent()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var customer = await Setup(buyer); (await Renewal(buyer, customer)).EnsureSuccessStatusCode();
        await Task.Delay(1100);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var end = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddMilliseconds(-50).ToUnixTimeMilliseconds());
        await db.Subscriptions.Where(item => item.Id == customer.Payment.SubscriptionId).ExecuteUpdateAsync(update => update.SetProperty(item => item.CurrentPeriodEnd, end));
        await new SubscriptionRenewalProcessor(db, new SubscriptionPaymentRecorder(db), TimeProvider.System,
            Options.Create(new BillingRenewalOptions { Enabled = true }), NullLogger<SubscriptionRenewalProcessor>.Instance).ProcessDueAsync(default);
        db.ChangeTracker.Clear(); var before = await db.Subscriptions.AsNoTracking().SingleAsync(item => item.Id == customer.Payment.SubscriptionId);
        Assert.Equal(end, before.CurrentPeriodStart);
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient()); var actor = await Actor(staff);
        var refunded = await Read<RefundView>(await Post(staff, actor.Token, customer.Payment.Id, 9.99m)); Assert.False(refunded.RevokedPremium);
        var current = await db.Subscriptions.AsNoTracking().SingleAsync(item => item.Id == customer.Payment.SubscriptionId);
        Assert.Equal("Active", current.Status); Assert.Equal(before.CurrentPeriodEnd, current.CurrentPeriodEnd);
        Assert.True((await db.BillingPreferences.SingleAsync(item => item.UserId == customer.User.Id)).AutoRenew);
        Assert.Equal("PREMIUM", (await Read<EntitlementView>(await buyer.SendAsync(HttpMethod.Get, "/api/v1/me/entitlements", bearer: customer.Token))).PlanCode);
    }

    [Fact]
    public async Task SqlRejectsOverRefundAndMissingAtomicCreditEvidence()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient()); var customer = await Setup(buyer);
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient()); var actor = await Actor(staff);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        foreach (decimal amount in new[] { 10m, 1m })
        {
            var now = DateTimeOffset.UtcNow;
            db.Refunds.Add(new Refund { PaymentId = customer.Payment.Id, Amount = amount, Reason = "Direct integrity fixture", Status = "Succeeded", IdempotencyKey = Guid.NewGuid(), CreatedBy = actor.User.Id, CreatedAt = now, CompletedAt = now });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
            db.ChangeTracker.Clear();
        }
    }

    [Fact]
    public async Task DelegatedAdminCanRefundAndReadOnlyFinancialAuditWithRemainingTotals()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient()); var customer = await Setup(buyer);
        using var rootBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var root = await Actor(rootBrowser);
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient()); var delegated = await Actor(staff, RoleNames.Admin);
        (await rootBrowser.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{delegated.User.Id}/billing-permission", new { enabled = true, reason = "Reviewed billing responsibility", currentPassword = SecurityTestAccount.Password, idempotencyKey = Guid.NewGuid() }, root.Token)).EnsureSuccessStatusCode();
        string token = await delegated.User.LoginAsync(staff, 1); Guid key = Guid.NewGuid();
        (await Post(staff, token, customer.Payment.Id, 4m, key)).EnsureSuccessStatusCode();
        var payments = await Read<AdministrationPage<ManagedPayment>>(await staff.SendAsync(HttpMethod.Get, $"/api/v1/admin/payments?paymentId={customer.Payment.Id}", bearer: token));
        Assert.Equal(4m, Assert.Single(payments.Items).RefundedAmount); Assert.Equal(9.99m, payments.Items[0].Amount);
        var subscriptions = await Read<AdministrationPage<ManagedSubscription>>(await staff.SendAsync(HttpMethod.Get, $"/api/v1/admin/subscriptions?userId={customer.User.Id}", bearer: token));
        Assert.Equal("Active", Assert.Single(subscriptions.Items).Status);
        var audit = await Read<AdministrationPage<BillingAuditItem>>(await staff.SendAsync(HttpMethod.Get, "/api/v1/admin/billing-audit", bearer: token));
        Assert.Contains(audit.Items, item => item.Action == "Billing.Refund.Succeeded");
        Assert.DoesNotContain(audit.Items, item => item.EntityType == "User");
        Assert.Equal(HttpStatusCode.NotFound, (await rootBrowser.SendAsync(HttpMethod.Get, $"/api/v1/admin/refunds/by-request/{key}", bearer: root.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.SendAsync(HttpMethod.Get, "/api/v1/admin/payments?page=0", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.SendAsync(HttpMethod.Get, "/api/v1/admin/payments?status=Unknown", bearer: token)).StatusCode);
    }

    [Fact]
    public async Task RefundOfAnExpiredSeparateSubscriptionPreservesNewSubscriptionConsent()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateRenewalClient(TimeProvider.System)); var customer = await Setup(buyer);
        await Task.Delay(1100);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var end = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddMilliseconds(-50).ToUnixTimeMilliseconds());
        await db.Subscriptions.Where(item => item.Id == customer.Payment.SubscriptionId).ExecuteUpdateAsync(update => update.SetProperty(item => item.CurrentPeriodEnd, end));
        var fresh = await Buy(buyer, customer.Token, customer.CardId);
        (await Renewal(buyer, customer with { Payment = fresh })).EnsureSuccessStatusCode();
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient()); var actor = await Actor(staff);
        Assert.False((await Read<RefundView>(await Post(staff, actor.Token, customer.Payment.Id, 9.99m))).RevokedPremium);
        var current = await Read<SubscriptionView>(await buyer.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: customer.Token));
        Assert.Equal(fresh.SubscriptionId, current.Id); Assert.Equal("PREMIUM", current.PlanCode); Assert.True(current.AutoRenew);
        Assert.Equal("Refunded", (await db.Subscriptions.SingleAsync(item => item.Id == customer.Payment.SubscriptionId)).Status);
       
    }
}

