using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class SubscriptionPaymentService(LexiGoDbContext database, BillingAccountAccess access, SubscriptionQueries queries, TimeProvider clock, SubscriptionPaymentRecorder recorder, PurchaseReceiptNotice notices)
{
    public async Task<PaymentOutcome> PayAsync(Guid userId, Guid familyId, Guid orderId, Guid cardId, Guid key, bool technicalFailure, CancellationToken cancellationToken)
    {
        if (key == Guid.Empty) return new(Error: "Use a valid payment request key.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var order = await database.BillingOrders.SingleOrDefaultAsync(item => item.Id == orderId && item.UserId == userId, cancellationToken);
        if (order is null) return new(Missing: true);
        if (order.OrderType != "Purchase") return new(Error: "Renewal orders must be processed by the renewal workflow.");
        var previous = await database.PaymentAttempts.SingleOrDefaultAsync(item => item.UserId == userId && item.IdempotencyKey == key, cancellationToken);
        if (previous is not null) return previous.OrderId == orderId && previous.TestCardId == cardId && previous.SimulateTechnicalFailure == technicalFailure
            ? new(await queries.PaymentAsync(userId, previous.Id, cancellationToken)) : new(Error: "This payment key belongs to different details.");
        var card = await database.TestCards.SingleOrDefaultAsync(item => item.Id == cardId && item.UserId == userId, cancellationToken);
        if (card is null) return new(Missing: true);
        if (order.Status is "Paid" or "Cancelled") return new(Error: "This order cannot accept another payment.");
        if (await database.PaymentAttempts.AnyAsync(item => item.OrderId == orderId && item.Status == "Pending", cancellationToken))
            return new(Error: "A payment for this order is already processing.");
        var now = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds());
        if (await database.PaymentAttempts.CountAsync(item => item.UserId == userId && item.CreatedAt > now.AddMinutes(-10), cancellationToken) >= 20) return new(RateLimited: true);
        bool expired = order.ExpiresAt <= now || order.Status == "Expired";
        if (expired) order.Status = "Expired";
        string? failure = expired ? "OrderExpired" : await queries.CurrentAsync(userId, cancellationToken) is not null ? "AlreadySubscribed"
            : card.Status != "Active" ? "CardBlocked" : card.Currency != order.Currency ? "CurrencyMismatch"
            : card.Balance < order.AmountSnapshot ? "InsufficientBalance" : technicalFailure ? "TechnicalFailure" : null;
        var payment = new PaymentAttempt
        {
            UserId = userId,
            OrderId = order.Id,
            TestCardId = card.Id,
            IdempotencyKey = key,
            Amount = order.AmountSnapshot,
            Currency = order.Currency,
            SimulateTechnicalFailure = technicalFailure,
            Status = failure is null ? "Succeeded" : "Failed",
            FailureCode = failure,
            CreatedAt = now,
            CompletedAt = now
        };
        Subscription? subscription = null;
        if (failure is null)
        {
            // Release the filtered one-active-subscription index before inserting the next paid period.
            var ended = await database.Subscriptions.Where(item => item.UserId == userId && item.Status == "Active" && item.CurrentPeriodEnd <= now).ToArrayAsync(cancellationToken);
            foreach (var item in ended)
            {
                item.Status = "Expired";
                database.SubscriptionEvents.Add(new SubscriptionEvent { SubscriptionId = item.Id, EventType = "Expired", OccurredAt = now, ActorId = userId });
            }
            if (ended.Length > 0) await database.SaveChangesAsync(cancellationToken);
            var price = await database.PlanPrices.SingleAsync(item => item.Id == order.PlanPriceId, cancellationToken);
            subscription = new Subscription
            {
                UserId = userId,
                PlanId = price.PlanId,
                CurrentPlanPriceId = price.Id,
                Status = "Active",
                CreatedAt = now,
                CurrentPeriodStart = now,
                CurrentPeriodEnd = order.PeriodSnapshot == "Monthly" ? now.AddMonths(1) : now.AddYears(1)
            };
            database.Subscriptions.Add(subscription);
            var preference = await database.BillingPreferences.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (preference?.AutoRenew == true)
            {
                preference.AutoRenew = false;
                preference.RenewalConsentAt = null;
                preference.RenewalConsentSubscriptionId = null;
                database.SubscriptionEvents.Add(new SubscriptionEvent { SubscriptionId = subscription.Id, EventType = "RenewalDisabledForNewPurchase", ActorId = userId, OccurredAt = now });
            }
        }
        var receiptNumber = recorder.Record(payment, order, card, subscription, "Activated", userId);
        if (payment.Status == "Succeeded" && subscription is not null && receiptNumber is not null)
            await notices.QueueAsync(payment, subscription, receiptNumber, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(SubscriptionQueries.View(payment, order.SubscriptionId, receiptNumber));
    }
}
