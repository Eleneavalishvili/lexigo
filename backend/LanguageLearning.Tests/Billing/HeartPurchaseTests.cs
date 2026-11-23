using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Billing;

public sealed class HeartPurchaseTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string Root = "/api/v1/me/hearts/purchases";
    private sealed record State(SecurityTestAccount User, string Token, Guid CardId);
    private async Task<State> Setup(SessionTestBrowser browser, int count = 0, decimal funds = 10, string currency = "GEL", bool expired = false)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        string token = await user.LoginAsync(browser);
        var created = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/test-cards", new { nickname = "Heart checkout", currency, idempotencyKey = Guid.NewGuid() }, token);
        created.EnsureSuccessStatusCode(); Guid cardId = (await created.Content.ReadFromJsonAsync<CardChange>())!.Card.Id;
        if (funds > 0) (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{cardId}/top-ups", new { amount = funds, idempotencyKey = Guid.NewGuid() }, token)).EnsureSuccessStatusCode();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        DateTimeOffset? refill = count == 0 ? DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 60) : null;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"LearnerHearts\" (\"UserId\",\"Count\",\"RefillAt\") VALUES ({user.Id},{count},{refill}) ON CONFLICT (\"UserId\") DO UPDATE SET \"Count\" = EXCLUDED.\"Count\", \"RefillAt\" = EXCLUDED.\"RefillAt\"");
        return new(user, token, cardId);
    }
    private static Task<HttpResponseMessage> Post(SessionTestBrowser browser, State state, Guid key, Guid? card = null, string offer = HeartPurchaseService.OfferCode) =>
        browser.SendAsync(HttpMethod.Post, Root, new { testCardId = card ?? state.CardId, offerCode = offer, idempotencyKey = key }, state.Token);
    private static async Task<HeartPurchaseView> Read(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<HeartPurchaseView>())!;
    }

    [Fact]
    public async Task FiveGelPurchaseRestoresSharedHeartsAndReplayDoesNotRestoreAgainAfterSpending()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser, count: 2);
        var content = await LearningTestContent.CreateAsync(fixture, state.User.Id);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, state.Token)).EnsureSuccessStatusCode();
        var sessionResponse = await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{content.LessonId}/sessions", bearer: state.Token);
        sessionResponse.EnsureSuccessStatusCode(); var session = (await sessionResponse.Content.ReadFromJsonAsync<SessionView>())!;
        var offerResponse = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/hearts/refill-offer", bearer: state.Token);
        offerResponse.EnsureSuccessStatusCode(); var offer = (await offerResponse.Content.ReadFromJsonAsync<HeartRefillOffer>())!;
        Assert.Equal(5m, offer.Amount); Assert.Equal("GEL", offer.Currency); Assert.Equal(2, offer.Hearts.Count);
        Guid key = Guid.NewGuid(); var bought = await Read(await Post(browser, state, key));
        Assert.Equal("Succeeded", bought.Status); Assert.Equal(5m, bought.ChargedAmount); Assert.Equal(2, bought.HeartsBefore); Assert.Equal(5, bought.HeartsAfter);
        var resumed = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: state.Token);
        resumed.EnsureSuccessStatusCode(); Assert.Equal(5, (await resumed.Content.ReadFromJsonAsync<SessionView>())!.Hearts);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise!.Id, answerText = "wrong", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        Assert.Equal(bought, await Read(await Post(browser, state, key)));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(4, (await db.LearnerHearts.SingleAsync(item => item.UserId == state.User.Id)).Count);
        Assert.Equal(5m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.Equal(1, await db.CardTransactions.CountAsync(item => item.TestCardId == state.CardId && item.Type == "Debit"));
        Assert.False(await db.XpTransactions.AnyAsync(item => item.UserId == state.User.Id));
        var history = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/transactions?cardId={state.CardId}&type=Debit", bearer: state.Token);
        history.EnsureSuccessStatusCode(); Assert.Equal(1, (await history.Content.ReadFromJsonAsync<BillingActivityPage>())!.TotalCount);
    }

    [Fact]
    public async Task ConcurrentSameAndDifferentKeysChargeOnlyOneRefill()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        Guid key = Guid.NewGuid(); var responses = await Task.WhenAll(Post(browser, state, key), Post(browser, state, key), Post(browser, state, Guid.NewGuid()));
        var first = await Read(responses[0]); var replay = await Read(responses[1]); await Read(responses[2]); Assert.Equal(first, replay);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await db.HeartRefillPurchases.CountAsync(item => item.UserId == state.User.Id && item.Status == "Succeeded"));
        Assert.Equal(1, await db.CardTransactions.CountAsync(item => item.TestCardId == state.CardId && item.Type == "Debit"));
        Assert.Equal(5m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(browser, state, key, offer: "changed-offer")).StatusCode);
    }

    [Theory]
    [InlineData("CardBlocked", 0, 10, "GEL")]
    [InlineData("InsufficientBalance", 0, 0, "GEL")]
    [InlineData("CurrencyMismatch", 0, 10, "USD")]
    [InlineData("HeartsAlreadyFull", 5, 10, "GEL")]
    public async Task DeclinesAreRecordedWithoutDebitOrHeartChanges(string reason, int count, decimal funds, string currency)
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser, count, funds, currency);
        if (reason == "CardBlocked") (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/test-cards/{state.CardId}/block", new { idempotencyKey = Guid.NewGuid() }, state.Token)).EnsureSuccessStatusCode();
        Guid key = Guid.NewGuid(); var declined = await Read(await Post(browser, state, key));
        Assert.Equal("Failed", declined.Status); Assert.Equal(reason, declined.FailureCode); Assert.Equal(0, declined.ChargedAmount);
        Assert.Equal(count, declined.HeartsAfter); Assert.Equal(declined, await Read(await Post(browser, state, key)));
        Assert.Equal(declined, await Read(await browser.SendAsync(HttpMethod.Get, $"{Root}/by-request/{key}", bearer: state.Token)));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(funds, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
        Assert.False(await db.CardTransactions.AnyAsync(item => item.TestCardId == state.CardId && item.Type == "Debit"));
    }

    [Fact]
    public async Task FreeExpiryWinsBeforePurchaseAndNoChargeIsMade()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser, expired: true);
        var result = await Read(await Post(browser, state, Guid.NewGuid()));
        Assert.Equal("HeartsAlreadyFull", result.FailureCode); Assert.Equal(0, result.ChargedAmount);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var balance = await db.LearnerHearts.SingleAsync(item => item.UserId == state.User.Id);
        Assert.Equal(5, balance.Count); Assert.Null(balance.RefillAt);
        Assert.Equal(10m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
    }

    [Fact]
    public async Task ForeignCardsResultsAndForgedPricesAreRejected()
    {
        using var owner = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(owner); Guid key = Guid.NewGuid();
        await Read(await Post(owner, state, key));
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient()); var other = await Setup(outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(outsider, other, Guid.NewGuid(), state.CardId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, $"{Root}/by-request/{key}", bearer: other.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.SendAsync(HttpMethod.Post, Root, new { testCardId = state.CardId, offerCode = HeartPurchaseService.OfferCode, amount = 0, idempotencyKey = Guid.NewGuid() }, state.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(owner, state, Guid.NewGuid(), offer: "discounted")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await outsider.SendAsync(HttpMethod.Get, Root)).StatusCode);
        var history = await owner.SendAsync(HttpMethod.Get, Root, bearer: state.Token); history.EnsureSuccessStatusCode();
        Assert.Equal(1, (await history.Content.ReadFromJsonAsync<HeartPurchasePage>())!.TotalCount);
    }

    [Fact]
    public async Task EvidenceIsImmutableAndFailedPurchaseCannotHaveADebit()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser, funds: 0);
        var failed = await Read(await Post(browser, state, Guid.NewGuid()));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var update = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"HeartRefillPurchases\" SET \"Amount\" = 1 WHERE \"Id\" = {failed.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, update.SqlState);
        var debit = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CardTransactions\" (\"Id\",\"TestCardId\",\"HeartRefillPurchaseId\",\"Type\",\"Amount\",\"BalanceAfter\",\"Reference\",\"CreatedAt\") VALUES ({Guid.NewGuid()},{state.CardId},{failed.Id},'Debit',5,0,{'x' + Guid.NewGuid().ToString()},{DateTimeOffset.UtcNow})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, debit.SqlState);
        var orphan = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"HeartRefillPurchases\" (\"Id\",\"UserId\",\"TestCardId\",\"IdempotencyKey\",\"OfferCode\",\"Amount\",\"Currency\",\"Status\",\"Reference\",\"HeartsBefore\",\"HeartsAfter\",\"CreatedAt\") VALUES ({Guid.NewGuid()},{state.User.Id},{state.CardId},{Guid.NewGuid()},{HeartPurchaseService.OfferCode},5,'GEL','Succeeded',{'x' + Guid.NewGuid().ToString()},0,5,{DateTimeOffset.UtcNow})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, orphan.SqlState);
        var missingReason = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"HeartRefillPurchases\" (\"Id\",\"UserId\",\"TestCardId\",\"IdempotencyKey\",\"OfferCode\",\"Amount\",\"Currency\",\"Status\",\"Reference\",\"HeartsBefore\",\"HeartsAfter\",\"CreatedAt\") VALUES ({Guid.NewGuid()},{state.User.Id},{state.CardId},{Guid.NewGuid()},{HeartPurchaseService.OfferCode},5,'GEL','Failed',{'x' + Guid.NewGuid().ToString()},0,0,{DateTimeOffset.UtcNow})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, missingReason.SqlState);
    }

    [Fact]
    public async Task AccountAttemptQuotaLimitsNewRowsButStillAllowsResultReplay()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await Setup(browser, count: 5);
        Guid firstKey = Guid.NewGuid();
        var first = await Read(await Post(browser, state, firstKey));
        for (int index = 1; index < 20; index++) await Read(await Post(browser, state, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(browser, state, Guid.NewGuid())).StatusCode);
        Assert.Equal(first, await Read(await Post(browser, state, firstKey)));
    }

    [Fact]
    public async Task LedgerFailureRollsBackPurchaseHeartsMoneyAndAudit()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var state = await Setup(browser);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fail_heart_debit_fixture() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Deliberate isolated debit failure.'; END; $body$;
            CREATE TRIGGER fail_heart_debit_fixture BEFORE INSERT ON "CardTransactions"
                FOR EACH ROW WHEN (NEW."Type" = 'Debit') EXECUTE FUNCTION fail_heart_debit_fixture();
            """);
        Guid key = Guid.NewGuid();
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await Post(browser, state, key)).StatusCode);
            Assert.Equal(10m, (await db.TestCards.SingleAsync(item => item.Id == state.CardId)).Balance);
            Assert.Equal(0, (await db.LearnerHearts.SingleAsync(item => item.UserId == state.User.Id)).Count);
            Assert.False(await db.HeartRefillPurchases.AnyAsync(item => item.UserId == state.User.Id));
            Assert.False(await db.AuditLogs.AnyAsync(item => item.ActorId == state.User.Id && item.EntityType == "HeartRefillPurchase"));
            Assert.Equal(HttpStatusCode.NotFound, (await browser.SendAsync(HttpMethod.Get, $"{Root}/by-request/{key}", bearer: state.Token)).StatusCode);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_heart_debit_fixture ON \"CardTransactions\"; DROP FUNCTION fail_heart_debit_fixture();"); }
        Assert.Equal("Succeeded", (await Read(await Post(browser, state, key))).Status);
    }
}
