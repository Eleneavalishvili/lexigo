using System.Security.Cryptography;
using System.Text;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Billing;

public sealed record RefundView(Guid Id, Guid PaymentId, decimal Amount, string Reason, string Status,
    DateTimeOffset? CompletedAt, bool RevokedPremium, string Currency);
public sealed record RefundOutcome(RefundView? Value = null, bool Forbidden = false, bool Missing = false, string? Error = null);

public sealed class RefundService(LexiGoDbContext database, BillingAdministrationAccess access, TimeProvider clock)
{
    public async Task<RefundOutcome> RefundAsync(Guid actorId, Guid familyId, Guid paymentId, decimal amount, string reason,
        string password, Guid key, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken)) return new(Forbidden: true);
        if (key == Guid.Empty || amount <= 0 || amount > 10000 || decimal.Round(amount, 2) != amount
            || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 3 or > 500 || reason.Any(char.IsControl))
            return new(Error: "Use a positive two-decimal amount, a reason of 3–500 characters and a valid request key.");
        Guid? owner = await database.PaymentAttempts.Where(item => item.Id == paymentId).Select(item => (Guid?)item.UserId).SingleOrDefaultAsync(cancellationToken);
        if (owner is null) return new(Missing: true);
        if (owner == actorId) return new(Error: "Another authorized billing administrator must approve your own refund.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AdministrationLocks.AcquireAsync(database, [actorId, owner.Value], cancellationToken);
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken)) return new(Forbidden: true);
        if (!await access.RecentProofAsync(actorId, familyId, password, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(Error: "Use your current password and a two-factor sign-in from the last five minutes.");
        }
        reason = reason.Trim();
        var previous = await database.Refunds.SingleOrDefaultAsync(item => item.CreatedBy == actorId && item.IdempotencyKey == key, cancellationToken);
        string reasonFingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(reason)));
        if (previous is not null) return previous.PaymentId == paymentId && previous.Amount == amount
            && previous.ReasonFingerprint == reasonFingerprint
            ? new(View(previous, await CurrencyAsync(previous.PaymentId, cancellationToken))) : new(Error: "This refund key belongs to different details.");
        bool deletedBuyer = await database.Users.Where(user => user.Id == owner.Value)
            .Select(user => user.Status == "Deleted").SingleAsync(cancellationToken);
        if (deletedBuyer) reason = Refund.ErasedReason;
        var payment = await database.PaymentAttempts.SingleAsync(item => item.Id == paymentId && item.UserId == owner.Value, cancellationToken);
        if (payment.Status != "Succeeded") return new(Error: "Only a successful subscription payment can be refunded.");
        var order = await database.BillingOrders.SingleAsync(item => item.Id == payment.OrderId && item.UserId == owner.Value, cancellationToken);
        if (order.SubscriptionId is not { } subscriptionId || order.Status != "Paid") return new(Error: "This payment has no paid subscription period.");
        decimal refunded = await database.Refunds.Where(item => item.PaymentId == paymentId).SumAsync(item => item.Amount, cancellationToken);
        if (amount > payment.Amount - refunded) return new(Error: "The refund exceeds the remaining amount paid.");
        var now = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds());
        if (await database.Refunds.CountAsync(item => item.CreatedBy == actorId && item.CreatedAt > now.AddMinutes(-10), cancellationToken) >= 20)
            return new(Error: "Take a short break before processing more refunds.");
        var card = await database.TestCards.SingleAsync(item => item.Id == payment.TestCardId && item.UserId == owner.Value, cancellationToken);
        if (card.Currency != payment.Currency) return new(Error: "The original card no longer matches the saved payment currency.");
        var subscription = await database.Subscriptions.SingleAsync(item => item.Id == subscriptionId && item.UserId == owner.Value, cancellationToken);
        var paidStart = BillingPeriods.Start(order.OrderType, order.RenewalPeriodKey, payment.CreatedAt);
        if (paidStart is null) return new(Error: "The original paid-period evidence needs review before a refund.");
        bool full = refunded + amount == payment.Amount;
        bool samePeriod = paidStart is not null && subscription.CurrentPeriodStart == paidStart;
        bool revoked = full && samePeriod && subscription.Status == "Active" && subscription.CurrentPeriodStart <= now && now < subscription.CurrentPeriodEnd;
        if (full && samePeriod)
        {
            subscription.Status = "Refunded";
            subscription.CancelAtPeriodEnd = true;
            subscription.ScheduledPlanPriceId = null;
            var preference = await database.BillingPreferences.SingleOrDefaultAsync(item => item.UserId == owner.Value, cancellationToken);
            bool newerPeriod = await database.Subscriptions.AnyAsync(item => item.UserId == owner.Value && item.Id != subscription.Id
                && item.Status == "Active" && item.CurrentPeriodStart <= now && now < item.CurrentPeriodEnd, cancellationToken);
            if (preference is not null && (preference.RenewalConsentSubscriptionId == subscription.Id || preference.RenewalConsentSubscriptionId is null && !newerPeriod))
            {
                preference.AutoRenew = false;
                preference.RenewalConsentAt = null;
                preference.RenewalConsentSubscriptionId = null;
            }
        }
        var refund = new Refund
        {
            PaymentId = payment.Id,
            Amount = amount,
            Reason = reason,
            ReasonFingerprint = reasonFingerprint,
            Status = "Succeeded",
            IdempotencyKey = key,
            CreatedBy = actorId,
            CreatedAt = now,
            CompletedAt = now,
            RevokedPremium = revoked
        };
        database.Refunds.Add(refund);
        // User top-ups remain capped. Restoring previously paid funds must not be capped or redirected.
        card.Balance += amount;
        card.Version++;
        database.CardTransactions.Add(new CardTransaction
        {
            TestCardId = card.Id,
            RefundId = refund.Id,
            Type = "Refund",
            Amount = amount,
            BalanceAfter = card.Balance,
            Reference = "REF-" + refund.Id.ToString("N"),
            CreatedAt = now
        });
        database.SubscriptionEvents.Add(new SubscriptionEvent
        {
            SubscriptionId = subscription.Id,
            RefundId = refund.Id,
            RelatedPaymentId = payment.Id,
            EventType = full ? samePeriod ? "Refunded" : "PriorPeriodRefunded" : "PartiallyRefunded",
            ActorId = actorId,
            OccurredAt = now
        });
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = "Billing.Refund.Succeeded",
            EntityType = "Refund",
            EntityId = refund.Id.ToString(),
            Reason = reason,
            TraceId = key.ToString(),
            OccurredAt = now
        });
        if (!deletedBuyer) database.Notifications.Add(new Notification
        {
            UserId = owner.Value,
            Type = "PaymentRefunded",
            Title = "Your simulated refund is complete",
            Body = revoked ? "Your current paid period was fully refunded to its original test card. Premium and renewal have stopped; your learning progress is saved. No real money was used."
                : "A simulated refund was credited to the original test card. Your current paid access has not been shortened. View your transaction trail for details. No real money was used.",
            CreatedAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(View(refund, payment.Currency));
    }

    public async Task<RefundOutcome> ByRequestAsync(Guid actorId, Guid familyId, Guid key, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken)) return new(Forbidden: true);
        var refund = await database.Refunds.AsNoTracking().SingleOrDefaultAsync(item => item.CreatedBy == actorId && item.IdempotencyKey == key, cancellationToken);
        return refund is null ? new(Missing: true) : new(View(refund, await CurrencyAsync(refund.PaymentId, cancellationToken)));
    }
    private Task<string> CurrencyAsync(Guid paymentId, CancellationToken cancellationToken) =>
        database.PaymentAttempts.Where(item => item.Id == paymentId).Select(item => item.Currency).SingleAsync(cancellationToken);
    public static RefundView View(Refund refund, string currency) => new(refund.Id, refund.PaymentId, refund.Amount, refund.Reason, refund.Status, refund.CompletedAt, refund.RevokedPremium, currency);
}
