using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Billing;

public sealed partial class SubscriptionPurchaseTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task RetiringPremiumBlocksNewQuotesButHonorsSavedQuotesAndPaidAccess()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await Setup(buyer);
        var quote = await Read<BillingOrderView>(await Order(buyer, state));
        using var anotherBuyer = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var other = await Setup(anotherBuyer);
        var actor = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(actor.Id.ToString()))!;
        Assert.True((await users.AddToRoleAsync(user, RoleNames.SuperAdmin)).Succeeded);
        using var admin = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var token = await actor.LoginAsync(admin);
        var route = $"/api/v1/admin/plans/{BillingCatalogSeed.Premium}";
        var retire = new
        {
            name = "Premium",
            expectedName = "Premium",
            isActive = false,
            expectedIsActive = true,
            reason = "Pause simulated sales",
            currentPassword = SecurityTestAccount.Password,
            idempotencyKey = Guid.NewGuid()
        };
        var retired = await Read<BillingPlanSummary>(await admin.SendAsync(HttpMethod.Put, route, retire, token));
        Assert.False(retired.IsActive);
        try
        {
            var listed = await Read<PlanView[]>(await buyer.SendAsync(HttpMethod.Get, "/api/v1/plans"));
            Assert.DoesNotContain(listed, item => item.Code == "PREMIUM");
            Assert.Equal(HttpStatusCode.Conflict, (await Order(anotherBuyer, other)).StatusCode);
            var payment = await Read<PaymentView>(await Pay(buyer, state, quote.Id));
            Assert.Equal("Succeeded", payment.Status);
            var access = await Read<EntitlementView>(await buyer.SendAsync(HttpMethod.Get,
                "/api/v1/me/entitlements", bearer: state.Token));
            Assert.Equal("PREMIUM", access.PlanCode);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Put, route,
                retire with { idempotencyKey = Guid.NewGuid() }, token)).StatusCode);
        }
        finally
        {
            var reopen = retire with { isActive = true, expectedIsActive = false, idempotencyKey = Guid.NewGuid() };
            Assert.True((await Read<BillingPlanSummary>(await admin.SendAsync(HttpMethod.Put, route, reopen, token))).IsActive);
        }
        Assert.Equal(9.99m, (await Read<BillingOrderView>(await Order(anotherBuyer, other))).Amount);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed record State(SecurityTestAccount User, string Token, Guid CardId);
    private async Task<State> Setup(SessionTestBrowser browser, decimal funds = 100, string currency = "GEL")
    {
        var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(browser);
        var response = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards", new { nickname = "Premium test", currency, idempotencyKey = Guid.NewGuid() }, token);
        response.EnsureSuccessStatusCode(); var card = (await response.Content.ReadFromJsonAsync<CardChange>())!.Card;
        if (funds > 0) (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{card.Id}/top-ups", new { amount = funds, idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
        return new(user, token, card.Id);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static Task<HttpResponseMessage> Order(SessionTestBrowser browser, State state, Guid? key = null, Guid? price = null) => browser.SendAsync(HttpMethod.Post,
        "/api/v1/me/billing-orders", new { planPriceId = price ?? BillingCatalogSeed.Monthly, idempotencyKey = key ?? Guid.NewGuid() }, state.Token);
    private static Task<HttpResponseMessage> Pay(SessionTestBrowser browser, State state, Guid orderId, Guid? key = null, bool technical = false, Guid? card = null) => browser.SendAsync(HttpMethod.Post,
        $"/api/v1/me/billing-orders/{orderId}/payments", new { testCardId = card ?? state.CardId, idempotencyKey = key ?? Guid.NewGuid(), simulateTechnicalFailure = technical }, state.Token);

    [Fact]
    public async Task OwnerCanCancelUnpaidCheckoutWithoutAChargeAndRetrySafely()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var owner = await Setup(buyer);
        var order = await Read<BillingOrderView>(await Order(buyer, owner));
        var route = $"/api/v1/me/billing-orders/{order.Id}";
        using var otherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var other = await Setup(otherBrowser);
        Assert.Equal(HttpStatusCode.NotFound, (await otherBrowser.SendAsync(
            HttpMethod.Post, route + "/cancel", bearer: other.Token)).StatusCode);

        var cancelled = await Read<BillingOrderView>(await buyer.SendAsync(
            HttpMethod.Post, route + "/cancel", bearer: owner.Token));
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(cancelled, await Read<BillingOrderView>(await buyer.SendAsync(
            HttpMethod.Post, route + "/cancel", bearer: owner.Token)));
        Assert.Equal(cancelled, await Read<BillingOrderView>(await buyer.SendAsync(
            HttpMethod.Get, route, bearer: owner.Token)));
        Assert.Equal(HttpStatusCode.Conflict, (await Pay(buyer, owner, order.Id)).StatusCode);

        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.False(await database.PaymentAttempts.AnyAsync(item => item.OrderId == order.Id));
        Assert.Equal(100m, await database.TestCards.Where(item => item.Id == owner.CardId)
            .Select(item => item.Balance).SingleAsync());
        Assert.Equal(1, await database.AuditLogs.CountAsync(item =>
            item.EntityId == order.Id.ToString() && item.Action == "Billing.OrderCancelled"));
    }

    [Fact]
    public async Task PersistedPendingPaymentIsRecoverableAndPreventsCancellationOrAnotherPayment()
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var owner = await Setup(buyer);
        var order = await Read<BillingOrderView>(await Order(buyer, owner));
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var pending = new PaymentAttempt
        {
            UserId = owner.User.Id,
            OrderId = order.Id,
            TestCardId = owner.CardId,
            IdempotencyKey = Guid.NewGuid(),
            Amount = order.Amount,
            Currency = order.Currency,
            Status = "Pending",
            CreatedAt = order.CreatedAt
        };
        database.PaymentAttempts.Add(pending);
        await database.SaveChangesAsync();

        var lookup = await Read<PaymentView>(await buyer.SendAsync(HttpMethod.Get,
            $"/api/v1/me/payments/by-request/{pending.IdempotencyKey}", bearer: owner.Token));
        Assert.Equal("Pending", lookup.Status);
        Assert.Null(lookup.CompletedAt);
        Assert.Null(lookup.ReceiptNumber);
        Assert.Equal(lookup, await Read<PaymentView>(await Pay(buyer, owner, order.Id, pending.IdempotencyKey)));
        Assert.Equal(HttpStatusCode.Conflict, (await Pay(buyer, owner, order.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.SendAsync(HttpMethod.Post,
            $"/api/v1/me/billing-orders/{order.Id}/cancel", bearer: owner.Token)).StatusCode);
        Assert.Equal(1, await database.PaymentAttempts.CountAsync(item => item.OrderId == order.Id));
        Assert.Equal(100m, await database.TestCards.Where(item => item.Id == owner.CardId)
            .Select(item => item.Balance).SingleAsync());
    }

    [Fact]
    public async Task VoicePracticeRequiresActivePremiumAndChecksAllFourLanguages()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await Setup(browser);
        const string route = "/api/v1/me/voice-practice";
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, route + "/es")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await browser.SendAsync(HttpMethod.Get, route + "/es", bearer: state.Token)).StatusCode);
        var order = await Read<BillingOrderView>(await Order(browser, state));
        await Read<PaymentView>(await Pay(browser, state, order.Id));

        foreach (string language in new[] { "es", "it", "de", "ru" })
        {
            using var response = await browser.SendAsync(HttpMethod.Get, route + "/" + language, bearer: state.Token);
            var words = await Read<LanguageLearning.Api.Learning.VoicePracticeItem[]>(response);
            Assert.Equal(3, words.Length);
            Assert.Equal(3, words.Select(word => word.Phrase).Distinct().Count());
            Assert.All(words, word => Assert.Equal(3, word.Choices.Length));
            Assert.True(response.Headers.CacheControl?.NoStore);
            string answerRoute = $"{route}/{language}/0/answers";
            var correct = await Read<LanguageLearning.Api.Learning.VoicePracticeFeedback>(
                await browser.SendAsync(HttpMethod.Post, answerRoute, new { choice = "coffee" }, state.Token));
            Assert.True(correct.Correct);
            var wrong = await Read<LanguageLearning.Api.Learning.VoicePracticeFeedback>(
                await browser.SendAsync(HttpMethod.Post, answerRoute, new { choice = "milk" }, state.Token));
            Assert.False(wrong.Correct);
            Assert.Equal("coffee", wrong.Meaning);
        }

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await database.Subscriptions.Where(item => item.UserId == state.User.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.CurrentPeriodStart, DateTimeOffset.UtcNow.AddMonths(-2))
            .SetProperty(item => item.CurrentPeriodEnd, DateTimeOffset.UtcNow.AddMonths(-1)));
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.SendAsync(HttpMethod.Post,
            route + "/es/0/answers", new { choice = "coffee" }, state.Token)).StatusCode);
    }

    [Fact]
    public async Task CatalogSeedsDocumentedPricesAndKeepsCoreListeningFree()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var plans = await Read<PlanView[]>(await browser.SendAsync(HttpMethod.Get, "/api/v1/plans"));
        var free = Assert.Single(plans, item => item.Code == "FREE"); var premium = Assert.Single(plans, item => item.Code == "PREMIUM");
        Assert.Empty(free.Prices); Assert.Contains(free.Features, item => item.Code == "CoreListening" && item.Available);
        Assert.Equal(9.99m, Assert.Single(premium.Prices, item => item.Period == "Monthly").Amount);
        Assert.Equal(79.99m, Assert.Single(premium.Prices, item => item.Period == "Annual").Amount);
        Assert.Contains(premium.Features, item => item.Code == "UnlimitedHearts" && item.Available);
        Assert.Contains(premium.Features, item => item.Code == "CharacterConversations" && !item.Available);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/entitlements")).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.True(await new SubscriptionQueries(db, TimeProvider.System, Microsoft.Extensions.Options.Options.Create(new BillingRenewalOptions())).HasAsync(Guid.NewGuid(), "CoreListening", default));
    }

    [Fact]
    public async Task ExtraDialogueRequiresPremiumWhileFreeDialogueStaysPlayable()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await Setup(browser);
        Guid courseId = (await LearningTestContent.CreateAsync(fixture, state.User.Id)).CourseId;
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var free = new Dialogue { CourseId = courseId, Title = "Café", Scenario = "cafe", Status = "Published" };
        var premium = new Dialogue
        {
            CourseId = courseId,
            Title = "Lost and found",
            Scenario = "lost-and-found",
            Status = "Published",
            RequiresPremium = true
        };
        db.AddRange(free, premium);
        AddConversation(free, "What would you like?", "Tea, please.");
        AddConversation(premium, "What did you lose?", "I lost a bag.");
        await db.SaveChangesAsync();

        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId }, state.Token))
            .EnsureSuccessStatusCode();
        string listRoute = $"/api/v1/dialogues?courseId={courseId}";
        string premiumRoute = $"/api/v1/dialogues/{premium.Id}/sessions";
        var list = await Read<DialogueListing[]>(await browser.SendAsync(HttpMethod.Get, listRoute, bearer: state.Token));
        Assert.True(Assert.Single(list, item => item.Id == premium.Id).Locked);
        Assert.False(Assert.Single(list, item => item.Id == free.Id).Locked);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/dialogues/{free.Id}/sessions", bearer: state.Token))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await browser.SendAsync(HttpMethod.Post, premiumRoute, bearer: state.Token)).StatusCode);

        var order = await Read<BillingOrderView>(await Order(browser, state));
        await Read<PaymentView>(await Pay(browser, state, order.Id));
        list = await Read<DialogueListing[]>(await browser.SendAsync(HttpMethod.Get, listRoute, bearer: state.Token));
        Assert.False(Assert.Single(list, item => item.Id == premium.Id).Locked);
        var session = await Read<DialogueView>(await browser.SendAsync(HttpMethod.Post, premiumRoute, bearer: state.Token));
        Assert.Equal("What did you lose?", session.Current.Text);
        string completedRoute = $"/api/v1/me/dialogue-sessions/{session.Id}";
        var choice = new { choiceId = Assert.Single(session.Current.Choices).Id, idempotencyKey = Guid.NewGuid() };
        var completed = await Read<DialogueView>(await browser.SendAsync(HttpMethod.Post, completedRoute + "/choices",
            choice, state.Token));
        Assert.NotNull(completed.CompletedAt);
        var replay = await Read<DialogueView>(await browser.SendAsync(HttpMethod.Post, premiumRoute, bearer: state.Token));
        string unfinishedRoute = $"/api/v1/me/dialogue-sessions/{replay.Id}";
        await db.Subscriptions.Where(item => item.UserId == state.User.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.CurrentPeriodStart, DateTimeOffset.UtcNow.AddMonths(-2))
            .SetProperty(item => item.CurrentPeriodEnd, DateTimeOffset.UtcNow.AddMonths(-1)));
        list = await Read<DialogueListing[]>(await browser.SendAsync(HttpMethod.Get, listRoute, bearer: state.Token));
        var locked = Assert.Single(list, item => item.Id == premium.Id);
        Assert.True(locked.Locked);
        Assert.Null(locked.ResumeSessionId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await browser.SendAsync(HttpMethod.Get, unfinishedRoute, bearer: state.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await browser.SendAsync(HttpMethod.Post, unfinishedRoute + "/choices", choice, state.Token)).StatusCode);
        var history = await Read<DialogueView>(await browser.SendAsync(HttpMethod.Get, completedRoute, bearer: state.Token));
        Assert.Equal(completed.Id, history.Id);
        Assert.NotNull(history.CompletedAt);
        Assert.Equal(completed.Path, history.Path);

        void AddConversation(Dialogue dialogue, string prompt, string answer)
        {
            var opening = new DialogueNode { DialogueId = dialogue.Id, Text = prompt, IsStart = true };
            var ending = new DialogueNode { DialogueId = dialogue.Id, Text = "Of course.", IsTerminal = true };
            db.AddRange(opening, ending, new DialogueChoice
            {
                DialogueId = dialogue.Id,
                NodeId = opening.Id,
                NextNodeId = ending.Id,
                Text = answer,
                Feedback = "Clear response.",
                IsCorrect = true
            });
        }
    }

    [Fact]
    public async Task FocusedPracticeSeparatesRepeatedMistakesFromDueReviewAfterCheckout()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await Setup(browser);
        var content = await LearningTestContent.CreateAsync(fixture, state.User.Id, 2);
        var courseId = content.CourseId;
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId }, state.Token))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await StartFocus("repeated")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StartFocus("anything")).StatusCode);

        var order = await Read<BillingOrderView>(await Order(browser, state));
        await Read<PaymentView>(await Pay(browser, state, order.Id));
        var mistakes = await Read<MistakePage>(await browser.SendAsync(HttpMethod.Get,
            $"/api/v1/me/mistakes?courseId={courseId}", bearer: state.Token));
        Assert.True(mistakes.AdvancedPracticeAvailable);
        Assert.Equal(HttpStatusCode.Conflict, (await StartFocus("due")).StatusCode);

        for (int repetition = 0; repetition < 2; repetition++)
        {
            var lesson = await Read<SessionView>(await browser.SendAsync(HttpMethod.Post,
                $"/api/v1/lessons/{content.LessonId}/sessions", bearer: state.Token));
            string sessionRoute = $"/api/v1/me/learning-sessions/{lesson.Id}";
            foreach (string answer in new[] { "wrong", "Hola" })
            {
                (await browser.SendAsync(HttpMethod.Post, sessionRoute + "/answers", new
                {
                    sessionExerciseId = lesson.CurrentExercise!.Id,
                    answerText = answer,
                    selectedOptionIds = Array.Empty<Guid>(),
                    idempotencyKey = Guid.NewGuid()
                }, state.Token)).EnsureSuccessStatusCode();
                lesson = await Read<SessionView>(await browser.SendAsync(HttpMethod.Get, sessionRoute, bearer: state.Token));
            }
            (await browser.SendAsync(HttpMethod.Post, sessionRoute + "/complete", bearer: state.Token))
                .EnsureSuccessStatusCode();
        }

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var past = DateTimeOffset.UtcNow.AddDays(-2);
        await db.AnswerAttempts.Where(item => item.UserId == state.User.Id && item.IsCorrect)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.SubmittedAt, past));
        var repeated = await Read<SessionView>(await StartFocus("repeated"));
        var due = await Read<SessionView>(await StartFocus("due"));
        var basic = await Read<SessionView>(await StartFocus(null));
        Assert.Equal(1, repeated.TotalCount);
        Assert.Equal(1, due.TotalCount);
        Assert.Equal(2, basic.TotalCount);
        Assert.NotEqual(repeated.Id, due.Id);
        Assert.NotEqual(basic.Id, repeated.Id);
        Assert.Equal(repeated.Id, (await Read<SessionView>(await StartFocus("repeated"))).Id);
        Assert.Equal(content.ExerciseIds[0], await ExerciseId(repeated.Id));
        Assert.Equal(content.ExerciseIds[1], await ExerciseId(due.Id));

        await db.Subscriptions.Where(item => item.UserId == state.User.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.CurrentPeriodStart, DateTimeOffset.UtcNow.AddMonths(-2))
            .SetProperty(item => item.CurrentPeriodEnd, DateTimeOffset.UtcNow.AddMonths(-1)));
        string focusedRoute = $"/api/v1/me/learning-sessions/{repeated.Id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await StartFocus("repeated")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await browser.SendAsync(HttpMethod.Get, focusedRoute, bearer: state.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await browser.SendAsync(HttpMethod.Post, focusedRoute + "/answers", new
            {
                sessionExerciseId = repeated.CurrentExercise!.Id,
                answerText = "Hola",
                selectedOptionIds = Array.Empty<Guid>(),
                idempotencyKey = Guid.NewGuid()
            }, state.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await browser.SendAsync(HttpMethod.Post, focusedRoute + "/complete", bearer: state.Token)).StatusCode);
        Assert.Equal(basic.Id, (await Read<SessionView>(await StartFocus(null))).Id);

        Task<HttpResponseMessage> StartFocus(string? focus) => browser.SendAsync(HttpMethod.Post,
            "/api/v1/me/practice-sessions", new { courseId, mistakesOnly = true, focus }, state.Token);

        Task<Guid> ExerciseId(Guid sessionId) => db.SessionExercises.Where(item => item.SessionId == sessionId)
            .Select(item => item.ExerciseId).SingleAsync();
    }

    [Fact]
    public async Task WeekComparisonRequiresAnActivePremiumPurchaseWhileTheWeeklySummaryStaysFree()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await Setup(browser);
        const string route = "/api/v1/me/weekly-summary/comparison";
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.SendAsync(HttpMethod.Get, route, bearer: state.Token)).StatusCode);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/weekly-summary", bearer: state.Token)).EnsureSuccessStatusCode();

        var order = await Read<BillingOrderView>(await Order(browser, state));
        await Read<PaymentView>(await Pay(browser, state, order.Id));
        using var response = await browser.SendAsync(HttpMethod.Get, route, bearer: state.Token);
        var comparison = await Read<WeeklyComparison>(response);
        Assert.Equal(comparison.Selected.WeekStart.AddDays(-7), comparison.Previous.WeekStart);
        Assert.Equal(0, comparison.Selected.WeeksAgo);
        Assert.Equal(1, comparison.Previous.WeeksAgo);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await browser.SendAsync(HttpMethod.Get, route + "?weeksAgo=12", bearer: state.Token)).StatusCode);
    }

    [Theory]
    [InlineData(false, "Monthly", 9.99)]
    [InlineData(true, "Annual", 79.99)]
    public async Task PaymentAtomicallyActivatesCorrectPeriodWithDebitAndDownloadableOwnedReceipt(bool annual, string period, decimal amount)
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        var order = await Read<BillingOrderView>(await Order(browser, state, price: annual ? BillingCatalogSeed.Annual : BillingCatalogSeed.Monthly));
        Assert.Equal(amount, order.Amount); Assert.Equal(period, order.Period); Assert.Equal("Created", order.Status);
        Guid key = Guid.NewGuid(); var paid = await Read<PaymentView>(await Pay(browser, state, order.Id, key));
        Assert.Equal("Succeeded", paid.Status); Assert.NotNull(paid.SubscriptionId); Assert.NotNull(paid.ReceiptNumber);
        Assert.Equal(paid, await Read<PaymentView>(await Pay(browser, state, order.Id, key)));
        var subscription = await Read<SubscriptionView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/subscription", bearer: state.Token));
        Assert.Equal("PREMIUM", subscription.PlanCode); Assert.Equal(period, subscription.Period); Assert.False(subscription.AutoRenew);
        Assert.Equal(annual ? subscription.CurrentPeriodStart!.Value.AddYears(1) : subscription.CurrentPeriodStart!.Value.AddMonths(1), subscription.CurrentPeriodEnd);
        var receipt = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/payments/{paid.Id}/receipt", bearer: state.Token);
        receipt.EnsureSuccessStatusCode(); Assert.Contains("LEXIGO — PAYMENT RECEIPT", await receipt.Content.ReadAsStringAsync());
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(100 - amount, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.Equal(1, await db.CardTransactions.CountAsync(item => item.PaymentId == paid.Id));
        Assert.Equal(1, await db.SubscriptionEvents.CountAsync(item => item.RelatedPaymentId == paid.Id && item.EventType == "Activated"));
        Assert.Equal(1, await db.PaymentReceipts.CountAsync(item => item.PaymentId == paid.Id));
    }

    [Theory]
    [InlineData("CardBlocked", 100, "GEL", false)]
    [InlineData("InsufficientBalance", 0, "GEL", false)]
    [InlineData("CurrencyMismatch", 100, "USD", false)]
    [InlineData("TechnicalFailure", 100, "GEL", true)]
    public async Task FailedPaymentIsSavedUnchargedAndDoesNotGrantPremium(string reason, decimal funds, string currency, bool technical)
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser, funds, currency);
        if (reason == "CardBlocked") (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{state.CardId}/block", new { idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        var order = await Read<BillingOrderView>(await Order(browser, state)); Guid key = Guid.NewGuid();
        var failed = await Read<PaymentView>(await Pay(browser, state, order.Id, key, technical));
        Assert.Equal("Failed", failed.Status); Assert.Equal(reason, failed.FailureCode); Assert.Null(failed.ReceiptNumber);
        Assert.Equal(failed, await Read<PaymentView>(await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/payments/by-request/{key}", bearer: state.Token)));
        Assert.Equal(failed, await Read<PaymentView>(await Pay(browser, state, order.Id, key, technical)));
        Assert.Equal("FREE", (await Read<EntitlementView>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/entitlements", bearer: state.Token))).PlanCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/payments/{failed.Id}/receipt", bearer: state.Token)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(funds, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.False(await db.Subscriptions.AnyAsync(item => item.UserId == state.User.Id)); Assert.False(await db.CardTransactions.AnyAsync(item => item.PaymentId == failed.Id));
    }

    [Fact]
    public async Task ConcurrentOrdersAndKeysCannotCreateTwoSubscriptionsOrCharges()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        var first = await Read<BillingOrderView>(await Order(browser, state)); var second = await Read<BillingOrderView>(await Order(browser, state)); Guid key = Guid.NewGuid();
        var responses = await Task.WhenAll(Pay(browser, state, first.Id, key), Pay(browser, state, first.Id, key), Pay(browser, state, second.Id));
        var values = await Task.WhenAll(responses.Select(Read<PaymentView>)); Assert.Equal(values[0], values[1]);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await db.Subscriptions.CountAsync(item => item.UserId == state.User.Id && item.Status == "Active"));
        Assert.Equal(1, await db.PaymentAttempts.CountAsync(item => item.UserId == state.User.Id && item.Status == "Succeeded"));
        Assert.Equal(90.01m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.Equal(HttpStatusCode.Conflict, (await Pay(browser, state, first.Id, key, true)).StatusCode);
    }

    [Fact]
    public async Task WrongPriceOwnerCardOrderAndRecoveryKeysCannotBypassTheServer()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        Guid key = Guid.NewGuid();
        var requests = await Task.WhenAll(Order(browser, state, key), Order(browser, state, key));
        using var created = Assert.Single(requests, response => response.StatusCode == HttpStatusCode.Created);
        using var replay = Assert.Single(requests, response => response.StatusCode == HttpStatusCode.OK);
        var order = await Read<BillingOrderView>(created);
        Assert.Equal(order, await Read<BillingOrderView>(replay));
        Assert.NotNull(created.Headers.Location);
        string orderPath = created.Headers.Location.AbsolutePath;
        Assert.Equal($"/api/v1/me/billing-orders/{order.Id}", orderPath);
        using var detail = await browser.SendAsync(HttpMethod.Get, orderPath, bearer: state.Token);
        Assert.Equal(order, await Read<BillingOrderView>(detail));
        Assert.Equal(HttpStatusCode.Conflict, (await Order(browser, state, key, BillingCatalogSeed.Annual)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/billing-orders", new { planPriceId = BillingCatalogSeed.Monthly, amount = 0, idempotencyKey = Guid.NewGuid() }, state.Token)).StatusCode);
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient()); var other = await Setup(outsider);
        using var hidden = await outsider.SendAsync(HttpMethod.Get, orderPath, bearer: other.Token);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Pay(outsider, other, order.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Pay(browser, state, order.Id, card: other.CardId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, $"/api/v1/me/billing-orders/by-request/{key}", bearer: other.Token)).StatusCode);
        var paid = await Read<PaymentView>(await Pay(browser, state, order.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, $"/api/v1/me/payments/{paid.Id}/receipt", bearer: other.Token)).StatusCode);
    }

    [Fact]
    public async Task QuoteExpiresAfterThirtyMinutesAndCannotCharge()
    {
        var clock = new Clock(); using var browser = new SessionTestBrowser(fixture.CreateBillingClient(clock)); var state = await Setup(browser);
        var order = await Read<BillingOrderView>(await Order(browser, state)); clock.Now = order.ExpiresAt;
        var failed = await Read<PaymentView>(await Pay(browser, state, order.Id)); Assert.Equal("OrderExpired", failed.FailureCode);
        Assert.Equal("Expired", (await Read<BillingOrderView>(await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/billing-orders/{order.Id}", bearer: state.Token))).Status);
    }

    [Fact]
    public async Task PremiumPreventsHeartLossWithoutMutatingTheUnderlyingFreeBalance()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"LearnerHearts\" (\"UserId\",\"Count\",\"RefillAt\") VALUES ({state.User.Id},0,{DateTimeOffset.UtcNow.AddHours(1)}) ON CONFLICT (\"UserId\") DO UPDATE SET \"Count\" = 0, \"RefillAt\" = EXCLUDED.\"RefillAt\"");
        var order = await Read<BillingOrderView>(await Order(browser, state)); await Read<PaymentView>(await Pay(browser, state, order.Id));
        var status = await Read<HeartStatus>(await browser.SendAsync(HttpMethod.Get, "/api/v1/me/hearts", bearer: state.Token)); Assert.True(status.Unlimited); Assert.Equal(5, status.Count);
        var content = await LearningTestContent.CreateAsync(fixture, state.User.Id, count: 1);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, state.Token)).EnsureSuccessStatusCode();
        var session = await Read<SessionView>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: state.Token)); Assert.True(session.UnlimitedHearts);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise!.Id, answerText = "wrong", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        var completed = await Read<SessionView>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: state.Token));
        Assert.False(completed.Passed); Assert.Equal(0, completed.AwardedXp);
        Assert.Equal(0, await db.LearnerHearts.Where(item => item.UserId == state.User.Id).Select(item => item.Count).SingleAsync());
        var refill = await Read<HeartPurchaseView>(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/hearts/purchases", new { testCardId = state.CardId, offerCode = HeartPurchaseService.OfferCode, idempotencyKey = Guid.NewGuid() }, state.Token));
        Assert.Equal(0, refill.ChargedAmount); Assert.Equal("HeartsAlreadyFull", refill.FailureCode);
        var subscription = await db.Subscriptions.SingleAsync(item => item.UserId == state.User.Id);
        var boundary = new Clock { Now = subscription.CurrentPeriodEnd };
        Assert.False(await new SubscriptionQueries(db, boundary, Microsoft.Extensions.Options.Options.Create(new BillingRenewalOptions())).HasAsync(state.User.Id, "UnlimitedHearts", default));
        Assert.Equal("FREE", (await new SubscriptionQueries(db, boundary, Microsoft.Extensions.Options.Options.Create(new BillingRenewalOptions())).EntitlementsAsync(state.User.Id, default)).PlanCode);
    }

    [Fact]
    public async Task ExpiredActiveRowCanBeReplacedWithoutTwoActiveSubscriptions()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        var oldOrder = await Read<BillingOrderView>(await Order(browser, state));
        await Read<PaymentView>(await Pay(browser, state, oldOrder.Id));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Subscriptions.Where(item => item.UserId == state.User.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.CurrentPeriodStart, DateTimeOffset.UtcNow.AddMonths(-2))
            .SetProperty(item => item.CurrentPeriodEnd, DateTimeOffset.UtcNow.AddMonths(-1)));
        var nextOrder = await Read<BillingOrderView>(await Order(browser, state));
        Assert.Equal("Succeeded", (await Read<PaymentView>(await Pay(browser, state, nextOrder.Id))).Status);
        Assert.Equal(1, await db.Subscriptions.CountAsync(item => item.UserId == state.User.Id && item.Status == "Active"));
        Assert.Equal(1, await db.Subscriptions.CountAsync(item => item.UserId == state.User.Id && item.Status == "Expired"));
        Assert.Equal(80.02m, await db.TestCards.Where(item => item.Id == state.CardId).Select(item => item.Balance).SingleAsync());
    }

    [Fact]
    public async Task LedgerInsertFailureRollsBackSubscriptionMoneyOrderReceiptAndReplay()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        var order = await Read<BillingOrderView>(await Order(browser, state));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fail_subscription_debit_fixture() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Deliberate isolated subscription failure.'; END; $body$;
            CREATE TRIGGER fail_subscription_debit_fixture BEFORE INSERT ON "CardTransactions"
                FOR EACH ROW WHEN (NEW."PaymentId" IS NOT NULL) EXECUTE FUNCTION fail_subscription_debit_fixture();
            """);
        Guid key = Guid.NewGuid();
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await Pay(browser, state, order.Id, key)).StatusCode);
            Assert.Equal(100m, await db.TestCards.Where(item => item.Id == state.CardId).Select(item => item.Balance).SingleAsync());
            Assert.False(await db.Subscriptions.AnyAsync(item => item.UserId == state.User.Id));
            Assert.False(await db.PaymentAttempts.AnyAsync(item => item.UserId == state.User.Id));
            Assert.Equal("Created", await db.BillingOrders.Where(item => item.Id == order.Id).Select(item => item.Status).SingleAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/payments/by-request/{key}", bearer: state.Token)).StatusCode);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_subscription_debit_fixture ON \"CardTransactions\"; DROP FUNCTION fail_subscription_debit_fixture();"); }
        Assert.Equal("Succeeded", (await Read<PaymentView>(await Pay(browser, state, order.Id, key))).Status);
    }

    [Fact]
    public async Task RetiringAPriceCannotRewriteAnAlreadyQuotedAmount()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        var order = await Read<BillingOrderView>(await Order(browser, state));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        try
        {
            await db.PlanPrices.Where(item => item.Id == BillingCatalogSeed.Monthly).ExecuteUpdateAsync(update => update.SetProperty(item => item.ValidTo, DateTimeOffset.UtcNow));
            Assert.Equal(HttpStatusCode.Conflict, (await Order(browser, state)).StatusCode);
            var paid = await Read<PaymentView>(await Pay(browser, state, order.Id));
            Assert.Equal(9.99m, paid.Amount);
        }
        finally { await db.PlanPrices.Where(item => item.Id == BillingCatalogSeed.Monthly).ExecuteUpdateAsync(update => update.SetProperty(item => item.ValidTo, (DateTimeOffset?)null)); }
    }

    [Fact]
    public async Task FailedAttemptReplayStaysUnchangedAfterAnotherPaymentSucceeds()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser, funds: 0);
        var order = await Read<BillingOrderView>(await Order(browser, state)); Guid failedKey = Guid.NewGuid();
        var failed = await Read<PaymentView>(await Pay(browser, state, order.Id, failedKey));
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{state.CardId}/top-ups", new { amount = 100, idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        Assert.Equal("Succeeded", (await Read<PaymentView>(await Pay(browser, state, order.Id))).Status);
        Assert.Equal(failed, await Read<PaymentView>(await Pay(browser, state, order.Id, failedKey)));
        Assert.Null(failed.SubscriptionId);
    }

    [Fact]
    public async Task DatabaseRejectsOrphanSuccessfulPaymentsAndDebitsOnFailedAttempts()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        var order = await Read<BillingOrderView>(await Order(browser, state));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var orphan = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"PaymentAttempts\" (\"Id\",\"UserId\",\"OrderId\",\"TestCardId\",\"Status\",\"IdempotencyKey\",\"Amount\",\"Currency\",\"SimulateTechnicalFailure\",\"CreatedAt\",\"CompletedAt\") VALUES ({Guid.NewGuid()},{state.User.Id},{order.Id},{state.CardId},'Succeeded',{Guid.NewGuid()},9.99,'GEL',false,{DateTimeOffset.UtcNow},{DateTimeOffset.UtcNow})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, orphan.SqlState);
        var failed = await Read<PaymentView>(await Pay(browser, state, order.Id, technical: true));
        var debit = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CardTransactions\" (\"Id\",\"TestCardId\",\"PaymentId\",\"Type\",\"Amount\",\"BalanceAfter\",\"Reference\",\"CreatedAt\") VALUES ({Guid.NewGuid()},{state.CardId},{failed.Id},'Debit',9.99,90.01,{Guid.NewGuid().ToString()},{DateTimeOffset.UtcNow})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, debit.SqlState);
    }

    [Fact]
    public async Task ClosedQuotesPricesAndPaymentEvidenceCannotBeRewritten()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        var order = await Read<BillingOrderView>(await Order(browser, state)); var paid = await Read<PaymentView>(await Pay(browser, state, order.Id));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var quote = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BillingOrders\" SET \"AmountSnapshot\" = 1 WHERE \"Id\" = {order.Id}")); Assert.Equal(PostgresErrorCodes.CheckViolation, quote.SqlState);
        var payment = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PaymentAttempts\" SET \"Amount\" = 1 WHERE \"Id\" = {paid.Id}")); Assert.Equal(PostgresErrorCodes.CheckViolation, payment.SqlState);
        var price = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PlanPrices\" SET \"Amount\" = 1 WHERE \"Id\" = {BillingCatalogSeed.Monthly}")); Assert.Equal(PostgresErrorCodes.CheckViolation, price.SqlState);
    }
}
