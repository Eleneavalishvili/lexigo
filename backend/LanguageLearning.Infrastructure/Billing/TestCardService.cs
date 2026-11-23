using System.Security.Cryptography;
using System.Text.Json;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class TestCardService(LexiGoDbContext database, TimeProvider clock, BillingAccountAccess access)
{
    public Task<CardOutcome> CreateAsync(Guid userId, Guid familyId, string nickname, string currency, Guid key, CancellationToken cancellationToken)
    {
        nickname = nickname.Trim();
        if (!ValidName(nickname) || currency is not ("GEL" or "USD" or "EUR"))
            return Task.FromResult(new CardOutcome(Error: "Use a short card name and GEL, USD or EUR."));
        return ApplyAsync(userId, familyId, "CreateCard", key, new { nickname, currency }, async () =>
        {
            if (await database.TestCards.CountAsync(item => item.UserId == userId, cancellationToken) >= 10)
                return new(Error: "You can have up to ten test cards.");
            var card = new TestCard
            {
                UserId = userId,
                Nickname = nickname,
                Currency = currency,
                DisplayLastFour = RandomNumberGenerator.GetInt32(10000).ToString("D4"),
                CreatedAt = clock.GetUtcNow()
            };
            database.TestCards.Add(card);
            return new(new(await ViewAsync(card, cancellationToken)));
        }, cancellationToken);
    }

    public Task<CardOutcome> StatusAsync(Guid userId, Guid familyId, Guid cardId, bool blocked, Guid key, CancellationToken cancellationToken) =>
        ApplyAsync(userId, familyId, blocked ? "BlockCard" : "UnblockCard", key, new { cardId }, async () =>
        {
            var card = await OwnedAsync(userId, cardId, cancellationToken);
            if (card is null) return new(Missing: true);
            string status = blocked ? "Blocked" : "Active";
            if (card.Status != status) { card.Status = status; card.Version++; }
            return new(new(await ViewAsync(card, cancellationToken)));
        }, cancellationToken);

    public Task<CardOutcome> EditAsync(Guid userId, Guid familyId, Guid cardId, string nickname, bool makeDefault, Guid key, CancellationToken cancellationToken)
    {
        nickname = nickname.Trim();
        if (!ValidName(nickname)) return Task.FromResult(new CardOutcome(Error: "Use a card name of 1–40 characters."));
        return ApplyAsync(userId, familyId, "EditCard", key, new { cardId, nickname, makeDefault }, async () =>
        {
            var card = await OwnedAsync(userId, cardId, cancellationToken);
            if (card is null) return new(Missing: true);
            if (makeDefault && card.Status != "Active") return new(Error: "Unblock this test card before making it the default.");
            if (card.Nickname != nickname) { card.Nickname = nickname; card.Version++; }
            if (makeDefault)
            {
                var preference = await database.BillingPreferences.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
                if (preference is null)
                {
                    preference = new BillingPreference { UserId = userId };
                    database.BillingPreferences.Add(preference);
                }
                preference.DefaultTestCardId = card.Id;
            }
            return new(new(await ViewAsync(card, cancellationToken), null));
        }, cancellationToken);
    }

    public Task<CardOutcome> TopUpAsync(Guid userId, Guid familyId, Guid cardId, decimal amount, Guid key, CancellationToken cancellationToken)
    {
        if (amount is < 1 or > 1000 || decimal.Round(amount, 2) != amount)
            return Task.FromResult(new CardOutcome(Error: "Test top-ups must be 1–1000 with at most two decimal places."));
        return ApplyAsync(userId, familyId, "TopUp", key, new { cardId, amount }, async () =>
        {
            var card = await OwnedAsync(userId, cardId, cancellationToken);
            if (card is null) return new(Missing: true);
            if (card.Status != "Active") return new(Error: "Unblock this test card before topping it up.");
            if (card.Balance + amount > 10000) return new(Error: "A test card can hold at most 10000 simulated currency units.");
            card.Balance += amount;
            card.Version++;
            var entry = new CardTransaction
            {
                TestCardId = card.Id,
                Type = "TopUp",
                Amount = amount,
                BalanceAfter = card.Balance,
                Reference = "TEST-" + Guid.NewGuid().ToString("N"),
                CreatedAt = clock.GetUtcNow()
            };
            database.CardTransactions.Add(entry);
            return new(new(await ViewAsync(card, cancellationToken), TestCardQueries.View(entry, card.Currency)));
        }, cancellationToken);
    }

    private async Task<CardOutcome> ApplyAsync(Guid userId, Guid familyId, string operation, Guid key, object payload,
        Func<Task<CardOutcome>> change, CancellationToken cancellationToken)
    {
        if (key == Guid.Empty) return new(Error: "Use a valid request key.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        string request = JsonSerializer.Serialize(payload);
        var replay = await database.BillingOperations.SingleOrDefaultAsync(item => item.UserId == userId
            && item.Operation == operation && item.IdempotencyKey == key, cancellationToken);
        if (replay is not null)
        {
            bool same = System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(request),
                System.Text.Json.Nodes.JsonNode.Parse(replay.RequestSnapshot));
            return same ? new(JsonSerializer.Deserialize<CardChange>(replay.ResponseSnapshot)!)
                : new(Error: "This request key was already used for different details.");
        }
        var outcome = await change();
        if (outcome.Value is not { } value) return outcome;
        database.BillingOperations.Add(new BillingOperation
        {
            UserId = userId,
            Operation = operation,
            IdempotencyKey = key,
            RequestSnapshot = request,
            ResponseSnapshot = JsonSerializer.Serialize(value),
            CreatedAt = clock.GetUtcNow()
        });
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = userId,
            Action = "TestCard." + operation,
            EntityType = "TestCard",
            EntityId = value.Card.Id.ToString(),
            Reason = "Simulated wallet operation; no real money.",
            TraceId = key.ToString(),
            OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return outcome;
    }

    private Task<TestCard?> OwnedAsync(Guid userId, Guid cardId, CancellationToken cancellationToken) =>
        database.TestCards.SingleOrDefaultAsync(item => item.Id == cardId && item.UserId == userId, cancellationToken);

    private async Task<TestCardView> ViewAsync(TestCard card, CancellationToken cancellationToken)
    {
        var local = database.BillingPreferences.Local.SingleOrDefault(item => item.UserId == card.UserId);
        bool isDefault = local is not null ? local.DefaultTestCardId == card.Id : await database.BillingPreferences
            .AnyAsync(item => item.UserId == card.UserId && item.DefaultTestCardId == card.Id, cancellationToken);
        return new(card.Id, card.Nickname, card.DisplayLastFour, card.Currency, card.Balance, card.Status, card.Version, card.CreatedAt, isDefault);
    }

    private static bool ValidName(string name) => name.Length is >= 1 and <= 40 && !name.Any(char.IsControl);
}
