using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Billing;

public sealed record SubscriptionHistoryItem(Guid Id, string PlanCode, string Status, string Period,
    DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, bool IsCurrent, bool CancelAtPeriodEnd);
public sealed record SubscriptionHistoryPage(SubscriptionHistoryItem[] Items, int Page, int PageSize, int TotalCount);
public sealed record SubscriptionEventItem(Guid Id, string Type, DateTimeOffset OccurredAt, bool BySystem,
    Guid? PaymentId, decimal? Amount, string? Currency, string? Period, DateTimeOffset? PaidPeriodStart,
    DateTimeOffset? PaidPeriodEnd, string? StopReason);
public sealed record SubscriptionEventsPage(SubscriptionEventItem[] Items, int Page, int PageSize, int TotalCount);

public sealed class SubscriptionHistoryQueries(LexiGoDbContext database, TimeProvider clock)
{
    public async Task<SubscriptionHistoryPage> ListAsync(Guid userId, int page, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var query = from subscription in database.Subscriptions.AsNoTracking()
                    join plan in database.SubscriptionPlans on subscription.PlanId equals plan.Id
                    join price in database.PlanPrices on subscription.CurrentPlanPriceId equals price.Id
                    where subscription.UserId == userId
                    select new { Subscription = subscription, plan.Code, price.Period };
        int count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.Subscription.CreatedAt).ThenByDescending(item => item.Subscription.Id)
            .Skip((page - 1) * 20).Take(20).Select(item => new SubscriptionHistoryItem(item.Subscription.Id, item.Code,
                item.Subscription.Status == "Active" && item.Subscription.CurrentPeriodEnd <= now ? "Expired" : item.Subscription.Status,
                item.Period, item.Subscription.CurrentPeriodStart, item.Subscription.CurrentPeriodEnd,
                item.Subscription.Status == "Active" && item.Subscription.CurrentPeriodStart <= now && now < item.Subscription.CurrentPeriodEnd,
                item.Subscription.CancelAtPeriodEnd)).ToArrayAsync(cancellationToken);
        return new(rows, page, 20, count);
    }

    public async Task<SubscriptionEventsPage?> EventsAsync(Guid userId, Guid subscriptionId, int page, CancellationToken cancellationToken)
    {
        if (!await database.Subscriptions.AnyAsync(item => item.Id == subscriptionId && item.UserId == userId, cancellationToken)) return null;
        var query = from entry in database.SubscriptionEvents.AsNoTracking()
                    join subscription in database.Subscriptions on entry.SubscriptionId equals subscription.Id
                    where subscription.Id == subscriptionId && subscription.UserId == userId
                    join payment in database.PaymentAttempts.Where(item => item.UserId == userId && item.Status == "Succeeded") on entry.RelatedPaymentId equals (Guid?)payment.Id into payments
                    from payment in payments.DefaultIfEmpty()
                    join order in database.BillingOrders.Where(item => item.UserId == userId) on payment.OrderId equals order.Id into orders
                    from order in orders.DefaultIfEmpty()
                    join refund in database.Refunds on entry.RefundId equals (Guid?)refund.Id into refunds
                    from refund in refunds.DefaultIfEmpty()
                    select new
                    {
                        entry.Id,
                        entry.EventType,
                        entry.OccurredAt,
                        BySystem = entry.ActorId == null,
                        PaymentId = payment.Status == "Succeeded" ? (Guid?)payment.Id : null,
                        Amount = entry.RefundId != null ? (decimal?)refund.Amount : (decimal?)payment.Amount,
                        payment.Currency,
                        Period = order.PeriodSnapshot,
                        PaymentAt = (DateTimeOffset?)payment.CreatedAt,
                        order.OrderType,
                        order.RenewalPeriodKey,
                        StopReason = database.BillingJobExecutions.Where(job => job.SubscriptionId == subscriptionId && job.LastAttemptAt == entry.OccurredAt)
                            .Select(job => job.FailureCode).FirstOrDefault()
                    };
        int count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * 20).Take(20).ToArrayAsync(cancellationToken);
        var items = rows.Select(row =>
        {
            DateTimeOffset? start = row.EventType is "Activated" or "Renewed" ? BillingPeriods.Start(row.OrderType, row.RenewalPeriodKey, row.PaymentAt) : null;
            var end = BillingPeriods.End(start, row.Period);
            return new SubscriptionEventItem(row.Id, row.EventType, row.OccurredAt, row.BySystem, row.PaymentId,
                row.Amount, row.Currency, row.Period, start, end, row.StopReason);
        }).ToArray();
        return new(items, page, 20, count);
    }
}
