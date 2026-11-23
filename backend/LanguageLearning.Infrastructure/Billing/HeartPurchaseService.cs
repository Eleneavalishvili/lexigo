using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class HeartPurchaseService(LexiGoDbContext database, BillingAccountAccess access, HeartBalanceService hearts, TimeProvider clock)
{
    public const string OfferCode = "full-hearts-gel-v1";
    public const decimal Price = 5m;
    public const string Currency = "GEL";

    public async Task<HeartRefillOffer> OfferAsync(Guid userId, CancellationToken cancellationToken) =>
        new(OfferCode, Price, Currency, HeartRecovery.MaximumHearts, await hearts.StatusAsync(userId, cancellationToken));

    public async Task<HeartPurchaseOutcome> PurchaseAsync(Guid userId, Guid familyId, Guid cardId, string offerCode, Guid key, CancellationToken cancellationToken)
    {
        if (key == Guid.Empty) return new(Error: "Use a valid purchase request key.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var previous = await database.HeartRefillPurchases.SingleOrDefaultAsync(item => item.UserId == userId && item.IdempotencyKey == key, cancellationToken);
        if (previous is not null) return previous.TestCardId == cardId && previous.OfferCode == offerCode
            ? new(View(previous)) : new(Error: "This request key was used for different purchase details.");
        if (offerCode != OfferCode) return new(Error: "This offer has changed. Reload checkout before confirming.");
        var card = await database.TestCards.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == cardId, cancellationToken);
        if (card is null) return new(Missing: true);
        if (await database.HeartRefillPurchases.CountAsync(item => item.UserId == userId && item.CreatedAt > clock.GetUtcNow().AddMinutes(-10), cancellationToken) >= 20)
            return new(RateLimited: true);
        var balance = await hearts.LockedAsync(userId, cancellationToken);
        string? failure = balance.Count == HeartRecovery.MaximumHearts ? "HeartsAlreadyFull"
            : card.Status != "Active" ? "CardBlocked"
            : card.Currency != Currency ? "CurrencyMismatch"
            : card.Balance < Price ? "InsufficientBalance" : null;
        var purchase = new HeartRefillPurchase
        {
            UserId = userId,
            TestCardId = card.Id,
            IdempotencyKey = key,
            OfferCode = OfferCode,
            Amount = Price,
            Currency = Currency,
            Status = failure is null ? "Succeeded" : "Failed",
            FailureCode = failure,
            Reference = "HEART-" + Guid.NewGuid().ToString("N"),
            HeartsBefore = balance.Count,
            HeartsAfter = failure is null ? HeartRecovery.MaximumHearts : balance.Count,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds())
        };
        database.HeartRefillPurchases.Add(purchase);
        if (failure is null)
        {
            card.Balance -= Price;
            card.Version++;
            hearts.Refill(balance);
            database.CardTransactions.Add(new CardTransaction
            {
                TestCardId = card.Id,
                HeartRefillPurchaseId = purchase.Id,
                Type = "Debit",
                Amount = Price,
                BalanceAfter = card.Balance,
                Reference = purchase.Reference,
                CreatedAt = purchase.CreatedAt
            });
        }
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = userId,
            Action = "Hearts.Purchase." + purchase.Status,
            EntityType = "HeartRefillPurchase",
            EntityId = purchase.Id.ToString(),
            Reason = failure ?? "Simulated full heart refill; no real money.",
            TraceId = key.ToString(),
            OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(View(purchase));
    }

    public async Task<HeartPurchaseView?> ByRequestAsync(Guid userId, Guid key, CancellationToken cancellationToken)
    {
        var purchase = await database.HeartRefillPurchases.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.IdempotencyKey == key, cancellationToken);
        return purchase is null ? null : View(purchase);
    }

    public async Task<HeartPurchasePage> HistoryAsync(Guid userId, int page, CancellationToken cancellationToken)
    {
        var query = database.HeartRefillPurchases.AsNoTracking().Where(item => item.UserId == userId);
        int total = await query.CountAsync(cancellationToken);
        var purchases = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * 20).Take(20).ToArrayAsync(cancellationToken);
        return new(purchases.Select(View).ToArray(), page, 20, total);
    }

    private static HeartPurchaseView View(HeartRefillPurchase purchase) => new(purchase.Id, purchase.TestCardId, purchase.OfferCode,
        purchase.Status, purchase.FailureCode, purchase.Amount, purchase.Status == "Succeeded" ? purchase.Amount : 0,
        purchase.Currency, purchase.Reference, purchase.HeartsBefore, purchase.HeartsAfter, purchase.CreatedAt);
}
