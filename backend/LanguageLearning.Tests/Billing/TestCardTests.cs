using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Billing;

public sealed class TestCardTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private static readonly string Root = "/api/v1/me/test-cards";
    private async Task<(SecurityTestAccount User, string Token)> Login(SessionTestBrowser browser)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        return (user, await user.LoginAsync(browser));
    }
    private static async Task<CardChange> Create(SessionTestBrowser browser, string token, Guid? key = null, string name = "Travel")
    {
        var response = await browser.SendAsync(HttpMethod.Post, Root, new { nickname = name, currency = "GEL", idempotencyKey = key ?? Guid.NewGuid() }, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CardChange>())!;
    }
    private static Task<HttpResponseMessage> TopUp(SessionTestBrowser browser, string token, Guid cardId, decimal amount, Guid? key = null) =>
        browser.SendAsync(HttpMethod.Post, $"{Root}/{cardId}/top-ups", new { amount, idempotencyKey = key ?? Guid.NewGuid() }, token);

    private static async Task<BillingActivityPage> History(SessionTestBrowser browser, string token, string path)
    {
        var response = await browser.SendAsync(HttpMethod.Get, path, bearer: token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BillingActivityPage>())!;
    }

    [Fact]
    public async Task CreationIsOwnedZeroBalanceMaskedAndReplaySafe()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var user = await Login(browser);
        Guid key = Guid.NewGuid(); var first = await Create(browser, user.Token, key); var replay = await Create(browser, user.Token, key);
        Assert.Equal(first, replay); Assert.Equal(0, first.Card.Balance); Assert.Equal("Active", first.Card.Status);
        Assert.Matches("^[0-9]{4}$", first.Card.DisplayLastFour);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, Root, new { nickname = "Different", currency = "GEL", idempotencyKey = key }, user.Token)).StatusCode);
        var list = (await (await browser.SendAsync(HttpMethod.Get, Root, bearer: user.Token)).Content.ReadFromJsonAsync<TestCardView[]>())!;
        Assert.Single(list); Assert.False(list[0].IsDefault);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Post, Root,
            new { nickname = "Real", currency = "GEL", balance = 999, cardNumber = "never accepted", idempotencyKey = Guid.NewGuid() }, user.Token)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentTopUpsAndReplaysHaveOneLedgerEntryPerOperation()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var user = await Login(browser); var card = (await Create(browser, user.Token)).Card;
        Guid key = Guid.NewGuid();
        var responses = await Task.WhenAll(TopUp(browser, user.Token, card.Id, 12.34m, key), TopUp(browser, user.Token, card.Id, 12.34m, key), TopUp(browser, user.Token, card.Id, 7.66m));
        foreach (var response in responses) response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await TopUp(browser, user.Token, card.Id, 12.35m, key)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(20m, (await db.TestCards.SingleAsync(item => item.Id == card.Id)).Balance);
        Assert.Equal(2, await db.CardTransactions.CountAsync(item => item.TestCardId == card.Id));
        Assert.Equal(2, await db.AuditLogs.CountAsync(item => item.ActorId == user.User.Id && item.Action == "TestCard.TopUp"));
    }

    [Fact]
    public async Task InvalidAmountsBlockedCardsAndWalletCapCannotCreateMoney()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var user = await Login(browser); var card = (await Create(browser, user.Token)).Card;
        foreach (decimal amount in new[] { 0m, -1m, 0.001m, 1000.01m }) Assert.Equal(HttpStatusCode.Conflict, (await TopUp(browser, user.Token, card.Id, amount)).StatusCode);
        (await browser.SendAsync(HttpMethod.Post, $"{Root}/{card.Id}/block", new { idempotencyKey = Guid.NewGuid() }, user.Token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await TopUp(browser, user.Token, card.Id, 1)).StatusCode);
        (await browser.SendAsync(HttpMethod.Post, $"{Root}/{card.Id}/unblock", new { idempotencyKey = Guid.NewGuid() }, user.Token)).EnsureSuccessStatusCode();
        for (int index = 0; index < 10; index++) (await TopUp(browser, user.Token, card.Id, 1000)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await TopUp(browser, user.Token, card.Id, 1)).StatusCode);
    }

    [Fact]
    public async Task CardRenameDefaultAndBlockingDoNotMoveBalances()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var user = await Login(browser);
        var first = (await Create(browser, user.Token)).Card; var second = (await Create(browser, user.Token)).Card;
        foreach (var card in new[] { first, second })
            (await browser.SendAsync(HttpMethod.Patch, $"{Root}/{card.Id}", new { nickname = "My journeys", makeDefault = true, idempotencyKey = Guid.NewGuid() }, user.Token)).EnsureSuccessStatusCode();
        var cards = (await (await browser.SendAsync(HttpMethod.Get, Root, bearer: user.Token)).Content.ReadFromJsonAsync<TestCardView[]>())!;
        Assert.Equal(second.Id, Assert.Single(cards, item => item.IsDefault).Id);
        Assert.All(cards, item => Assert.Equal(0, item.Balance));
        Guid block = Guid.NewGuid();
        (await browser.SendAsync(HttpMethod.Post, $"{Root}/{first.Id}/block", new { idempotencyKey = block }, user.Token)).EnsureSuccessStatusCode();
        (await browser.SendAsync(HttpMethod.Post, $"{Root}/{first.Id}/block", new { idempotencyKey = block }, user.Token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Patch, $"{Root}/{first.Id}", new { nickname = "Blocked default", makeDefault = true, idempotencyKey = Guid.NewGuid() }, user.Token)).StatusCode);
    }

    [Fact]
    public async Task ForeignCardsAndTransactionsAreNotExposed()
    {
        using var owner = new SessionTestBrowser(fixture.CreateIsolatedClient()); var user = await Login(owner); var card = (await Create(owner, user.Token)).Card;
        (await TopUp(owner, user.Token, card.Id, 10)).EnsureSuccessStatusCode();
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient()); var other = await Login(outsider);
        Assert.Empty((await (await outsider.SendAsync(HttpMethod.Get, Root, bearer: other.Token)).Content.ReadFromJsonAsync<TestCardView[]>())!);
        Assert.Equal(HttpStatusCode.NotFound, (await TopUp(outsider, other.Token, card.Id, 10)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Post, $"{Root}/{card.Id}/block", new { idempotencyKey = Guid.NewGuid() }, other.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Patch, $"{Root}/{card.Id}", new { nickname = "Stolen", makeDefault = true, idempotencyKey = Guid.NewGuid() }, other.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, $"{Root}/{card.Id}/transactions", bearer: other.Token)).StatusCode);
        Assert.Equal(0, (await History(outsider, other.Token, "/api/v1/me/transactions")).TotalCount);
        Assert.Equal(HttpStatusCode.Unauthorized, (await outsider.SendAsync(HttpMethod.Get, Root)).StatusCode);
    }

    [Fact]
    public async Task LedgerIsAppendOnlyAndDefaultCannotReferenceAnotherOwner()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var user = await Login(browser); var card = (await Create(browser, user.Token)).Card;
        (await TopUp(browser, user.Token, card.Id, 10)).EnsureSuccessStatusCode(); var stranger = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var update = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CardTransactions\" SET \"Amount\" = 1 WHERE \"TestCardId\" = {card.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, update.SqlState);
        var delete = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"CardTransactions\" WHERE \"TestCardId\" = {card.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, delete.SqlState);
        var negative = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TestCards\" SET \"Balance\" = -1 WHERE \"Id\" = {card.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, negative.SqlState);
        var foreign = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"BillingPreferences\" (\"UserId\",\"DefaultTestCardId\",\"AutoRenew\") VALUES ({stranger.Id},{card.Id},false)"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, foreign.SqlState);
    }

    [Fact]
    public async Task LedgerFailureRollsBackBalanceAuditAndReplayEvidence()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var user = await Login(browser);
        var card = (await Create(browser, user.Token)).Card;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_wallet_fixture_insert() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Deliberate isolated test failure.'; END; $body$;
            CREATE TRIGGER reject_wallet_fixture_insert BEFORE INSERT ON "CardTransactions"
                FOR EACH ROW WHEN (NEW."Amount" = 42) EXECUTE FUNCTION reject_wallet_fixture_insert();
            """);
        Guid key = Guid.NewGuid();
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await TopUp(browser, user.Token, card.Id, 42, key)).StatusCode);
            Assert.Equal(0, await db.TestCards.Where(item => item.Id == card.Id).Select(item => item.Balance).SingleAsync());
            Assert.False(await db.CardTransactions.AnyAsync(item => item.TestCardId == card.Id));
            Assert.False(await db.BillingOperations.AnyAsync(item => item.UserId == user.User.Id && item.Operation == "TopUp"));
            Assert.False(await db.AuditLogs.AnyAsync(item => item.ActorId == user.User.Id && item.Action == "TestCard.TopUp"));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_wallet_fixture_insert ON \"CardTransactions\"; DROP FUNCTION reject_wallet_fixture_insert();");
        }
        (await TopUp(browser, user.Token, card.Id, 42, key)).EnsureSuccessStatusCode();
        Assert.Equal(42, await db.TestCards.Where(item => item.Id == card.Id).Select(item => item.Balance).SingleAsync());
    }

    [Fact]
    public async Task HistoryIsFilteredStableAndBoundedAndCardCountIsLimited()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient()); var user = await Login(browser); var card = (await Create(browser, user.Token)).Card;
        for (int index = 0; index < 21; index++) (await TopUp(browser, user.Token, card.Id, 1)).EnsureSuccessStatusCode();
        var first = await History(browser, user.Token, $"/api/v1/me/transactions?cardId={card.Id}&type=TopUp&page=1");
        var second = await History(browser, user.Token, $"{Root}/{card.Id}/transactions?page=2");
        Assert.Equal(21, first.TotalCount); Assert.Equal(20, first.Items.Count); Assert.Single(second.Items);
        Assert.DoesNotContain(second.Items[0].Id, first.Items.Select(item => item.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/transactions?page=0", bearer: user.Token)).StatusCode);
        for (int index = 1; index < 10; index++) await Create(browser, user.Token);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, Root, new { nickname = "Eleventh", currency = "GEL", idempotencyKey = Guid.NewGuid() }, user.Token)).StatusCode);
    }
}
