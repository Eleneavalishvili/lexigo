using LanguageLearning.Core.Billing;
using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class BillingOrderService(LexiGoDbContext database, BillingAccountAccess access, SubscriptionQueries queries, TimeProvider clock)
{
    public async Task<OrderOutcome> CancelAsync(Guid userId, Guid familyId, Guid orderId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var order = await database.BillingOrders.SingleOrDefaultAsync(
            item => item.Id == orderId && item.UserId == userId, cancellationToken);
        if (order is null || order.OrderType != "Purchase") return new(Missing: true);

        var now = clock.GetUtcNow();
        if (order.Status == "Cancelled") return new(SubscriptionQueries.View(order, now));
        if (order.Status != "Created" || order.ExpiresAt <= now ||
            await database.PaymentAttempts.AnyAsync(item => item.OrderId == orderId && item.Status == "Pending", cancellationToken))
            return new(Error: "This checkout can no longer be cancelled.");

        order.Status = "Cancelled";
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = userId,
            Action = "Billing.OrderCancelled",
            EntityType = "BillingOrder",
            EntityId = order.Id.ToString(),
            Reason = "Learner cancelled an unpaid simulated checkout.",
            TraceId = order.Id.ToString(),
            OccurredAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(SubscriptionQueries.View(order, now));
    }

    public async Task<OrderOutcome> CreateAsync(Guid userId, Guid familyId, Guid priceId, Guid key, CancellationToken cancellationToken)
    {
        if (key == Guid.Empty) return new(Error: "Use a valid order request key.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var previous = await database.BillingOrders.SingleOrDefaultAsync(item => item.UserId == userId && item.IdempotencyKey == key, cancellationToken);
        if (previous is not null) return previous.PlanPriceId == priceId ? new(SubscriptionQueries.View(previous, clock.GetUtcNow()))
            : new(Error: "This order request key belongs to a different price.");
        var now = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds());
        if (await database.BillingOrders.CountAsync(item => item.UserId == userId && item.CreatedAt > now.AddMinutes(-10), cancellationToken) >= 20) return new(RateLimited: true);
        var price = await database.PlanPrices.SingleOrDefaultAsync(item => item.Id == priceId && item.ValidFrom <= now
            && (item.ValidTo == null || item.ValidTo > now), cancellationToken);
        if (price is null || !await database.SubscriptionPlans.AnyAsync(item => item.Id == price.PlanId && item.IsActive && item.Code == "PREMIUM", cancellationToken))
            return new(Error: "This price is no longer available. Review the current plans.");
        if (await queries.CurrentAsync(userId, cancellationToken) is not null) return new(Error: "You already have an active subscription.");
        var order = new BillingOrder
        {
            UserId = userId,
            PlanPriceId = price.Id,
            IdempotencyKey = key,
            AmountSnapshot = price.Amount,
            Currency = price.Currency,
            PeriodSnapshot = price.Period,
            Status = "Created",
            OrderType = "Purchase",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30)
        };
        database.BillingOrders.Add(order);
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = userId,
            Action = "Billing.OrderCreated",
            EntityType = "BillingOrder",
            EntityId = order.Id.ToString(),
            Reason = "Simulated subscription quote; no funds charged.",
            TraceId = key.ToString(),
            OccurredAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(SubscriptionQueries.View(order, now), Created: true);
    }
}
