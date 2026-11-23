using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Accounts.ExportJson;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class SimulatedBillingExport(LexiGoDbContext database)
{
    internal async Task WriteSectionsAsync(Guid userId, Stream destination, CancellationToken cancellationToken)
    {
        var cards = database.TestCards.AsNoTracking().Where(item => item.UserId == userId);
        var payments = database.PaymentAttempts.AsNoTracking().Where(item => item.UserId == userId);
        var subscriptions = database.Subscriptions.AsNoTracking().Where(item => item.UserId == userId);
        var settings = await database.BillingPreferences.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => new { item.DefaultTestCardId, item.AutoRenew, item.RenewalConsentAt })
            .SingleOrDefaultAsync(cancellationToken);

        await destination.WriteAsync(",\"simulatedBilling\":{\"mode\":\"simulated\""u8.ToArray(), cancellationToken);
        await WriteValueAsync(destination, ",\"preferences\":"u8.ToArray(), settings, cancellationToken);
        await WriteSectionAsync(destination, ",\"testCards\":"u8.ToArray(),
            cards.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
                .Select(item => new { item.Id, item.Nickname, item.DisplayLastFour, item.Currency, item.Balance, item.Status, item.CreatedAt }),
            cancellationToken);
        await WriteSectionAsync(destination, ",\"walletTransactions\":"u8.ToArray(),
            from item in database.CardTransactions.AsNoTracking()
            join card in cards on item.TestCardId equals card.Id
            orderby item.CreatedAt, item.Id
            select new { item.Id, item.TestCardId, item.Type, item.Amount, item.BalanceAfter, item.Reference, item.CreatedAt },
            cancellationToken);
        await WriteSectionAsync(destination, ",\"subscriptions\":"u8.ToArray(),
            from item in subscriptions
            join plan in database.SubscriptionPlans on item.PlanId equals plan.Id
            orderby item.CreatedAt, item.Id
            select new
            {
                item.Id, plan.Code, plan.Name, item.Status, item.CurrentPeriodStart, item.CurrentPeriodEnd,
                item.CancelAtPeriodEnd, item.CurrentPlanPriceId, item.ScheduledPlanPriceId, item.CreatedAt
            }, cancellationToken);
        await WriteSectionAsync(destination, ",\"subscriptionEvents\":"u8.ToArray(),
            from item in database.SubscriptionEvents.AsNoTracking()
            join subscription in subscriptions on item.SubscriptionId equals subscription.Id
            orderby item.OccurredAt, item.Id
            select new { item.SubscriptionId, item.EventType, item.OccurredAt }, cancellationToken);
        await WriteSectionAsync(destination, ",\"orders\":"u8.ToArray(),
            database.BillingOrders.AsNoTracking().Where(item => item.UserId == userId)
                .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
                .Select(item => new
                {
                    item.Id, item.SubscriptionId, item.AmountSnapshot, item.Currency, item.PeriodSnapshot,
                    item.OrderType, item.Status, item.CreatedAt, item.ExpiresAt
                }), cancellationToken);
        await WriteSectionAsync(destination, ",\"payments\":"u8.ToArray(),
            payments.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
                .Select(item => new
                {
                    item.Id, item.OrderId, item.TestCardId, item.Amount, item.Currency, item.Status,
                    item.FailureCode, item.CreatedAt, item.CompletedAt
                }), cancellationToken);
        await WriteSectionAsync(destination, ",\"receipts\":"u8.ToArray(),
            from item in database.PaymentReceipts.AsNoTracking()
            join payment in payments on item.PaymentId equals payment.Id
            orderby item.IssuedAt, item.Id
            select new { item.PaymentId, item.ReceiptNumber, item.IssuedAt }, cancellationToken);
        await WriteSectionAsync(destination, ",\"refunds\":"u8.ToArray(),
            from item in database.Refunds.AsNoTracking()
            join payment in payments on item.PaymentId equals payment.Id
            orderby item.CreatedAt, item.Id
            select new { item.Id, item.PaymentId, item.Amount, item.Status, item.CreatedAt, item.CompletedAt, item.RevokedPremium },
            cancellationToken);
        await WriteSectionAsync(destination, ",\"heartRefills\":"u8.ToArray(),
            database.HeartRefillPurchases.AsNoTracking().Where(item => item.UserId == userId)
                .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
                .Select(item => new
                {
                    item.TestCardId, item.OfferCode, item.Amount, item.Currency, item.Status, item.FailureCode,
                    item.Reference, item.HeartsBefore, item.HeartsAfter, item.CreatedAt
                }), cancellationToken);
        await destination.WriteAsync("}"u8.ToArray(), cancellationToken);
    }
}
