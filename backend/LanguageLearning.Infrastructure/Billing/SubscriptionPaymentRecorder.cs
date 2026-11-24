using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Data;

namespace LanguageLearning.Infrastructure.Billing;

// Both purchase and renewal callers hold the account lock and commit these artifacts together.
public sealed class SubscriptionPaymentRecorder(LexiGoDbContext database)
{
    public string? Record(PaymentAttempt payment, BillingOrder order, TestCard card, Subscription? subscription,
        string eventType, Guid? actorId)
    {
        if (payment.UserId != order.UserId || card.UserId != payment.UserId || payment.TestCardId != card.Id
            || payment.OrderId != order.Id || payment.Amount != order.AmountSnapshot || payment.Currency != order.Currency)
            throw new InvalidOperationException("Payment artifacts must describe the same owned quote.");
        string? receiptNumber = null;
        if (payment.Status == "Succeeded")
        {
            if (subscription is null || subscription.UserId != payment.UserId || card.Status != "Active" || card.Currency != payment.Currency
                || card.Balance < payment.Amount || eventType is not ("Activated" or "Renewed"))
                throw new InvalidOperationException("Successful payment artifacts require an eligible subscription and card.");
            order.Status = "Paid";
            order.SubscriptionId = subscription.Id;
            card.Balance -= payment.Amount;
            card.Version++;
            database.CardTransactions.Add(new CardTransaction
            {
                TestCardId = card.Id,
                PaymentId = payment.Id,
                Type = "Debit",
                Amount = payment.Amount,
                BalanceAfter = card.Balance,
                Reference = "PAY-" + payment.Id.ToString("N"),
                CreatedAt = payment.CreatedAt
            });
            database.SubscriptionEvents.Add(new SubscriptionEvent
            {
                SubscriptionId = subscription.Id,
                EventType = eventType,
                RelatedPaymentId = payment.Id,
                OccurredAt = payment.CreatedAt,
                ActorId = actorId
            });
            receiptNumber = "LXG-" + payment.Id.ToString("N");
            database.PaymentReceipts.Add(new PaymentReceipt { PaymentId = payment.Id, ReceiptNumber = receiptNumber, IssuedAt = payment.CreatedAt });
        }
        database.PaymentAttempts.Add(payment);
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = "Billing.Payment." + payment.Status,
            EntityType = "PaymentAttempt",
            EntityId = payment.Id.ToString(),
            Reason = payment.FailureCode ?? "Simulated subscription payment; no real money.",
            TraceId = payment.IdempotencyKey.ToString(),
            OccurredAt = payment.CreatedAt
        });
        return receiptNumber;
    }
}
