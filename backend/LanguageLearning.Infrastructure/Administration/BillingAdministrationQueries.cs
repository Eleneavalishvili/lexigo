using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed record ManagedPayment(Guid Id, Guid UserId, string Customer, Guid OrderId, Guid TestCardId, string CardStatus,
    string Status, string? FailureCode, decimal Amount, string Currency, decimal RefundedAmount, DateTimeOffset CreatedAt);
public sealed record ManagedSubscription(Guid Id, Guid UserId, string Customer, string PlanCode, string Status, string Period,
    DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, bool CancelAtPeriodEnd);
public sealed record BillingAuditItem(Guid Id, Guid? ActorId, string Action, string EntityType, string EntityId,
    string? Reason, string TraceId, DateTimeOffset OccurredAt);

public sealed class BillingAdministrationQueries(LexiGoDbContext database, TimeProvider clock)
{
    public async Task<AdministrationPage<ManagedPayment>> PaymentsAsync(Guid? userId, Guid? paymentId, string? status, DateTimeOffset? from,
        DateTimeOffset? to, int page, CancellationToken cancellationToken)
    {
        var query = database.PaymentAttempts.AsNoTracking().AsQueryable();
        if (userId is { } owner) query = query.Where(item => item.UserId == owner);
        if (paymentId is { } payment) query = query.Where(item => item.Id == payment);
        if (status is not null) query = query.Where(item => item.Status == status);
        if (from is { } start) query = query.Where(item => item.CreatedAt >= start.ToUniversalTime());
        if (to is { } end) query = query.Where(item => item.CreatedAt < end.ToUniversalTime());
        int count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Skip((page - 1) * 20).Take(20)
            .Select(item => new ManagedPayment(item.Id, item.UserId, database.Users.Where(user => user.Id == item.UserId).Select(user => user.DisplayName).Single(),
                item.OrderId, item.TestCardId, database.TestCards.Where(card => card.Id == item.TestCardId).Select(card => card.Status).Single(),
                item.Status, item.FailureCode, item.Amount, item.Currency,
                database.Refunds.Where(refund => refund.PaymentId == item.Id).Sum(refund => refund.Amount), item.CreatedAt)).ToArrayAsync(cancellationToken);
        return new(count, page, 20, rows);
    }

    public async Task<AdministrationPage<ManagedSubscription>> SubscriptionsAsync(Guid? userId, string? status, int page, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var query = database.Subscriptions.AsNoTracking().AsQueryable();
        if (userId is { } owner) query = query.Where(item => item.UserId == owner);
        if (status == "Expired") query = query.Where(item => item.Status == "Expired" || item.Status == "Active" && item.CurrentPeriodEnd <= now);
        else if (status == "Active") query = query.Where(item => item.Status == "Active" && item.CurrentPeriodStart <= now && now < item.CurrentPeriodEnd);
        else if (status is not null) query = query.Where(item => item.Status == status);
        int count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Skip((page - 1) * 20).Take(20)
            .Select(item => new ManagedSubscription(item.Id, item.UserId, database.Users.Where(user => user.Id == item.UserId).Select(user => user.DisplayName).Single(),
                database.SubscriptionPlans.Where(plan => plan.Id == item.PlanId).Select(plan => plan.Code).Single(),
                item.Status == "Active" && item.CurrentPeriodEnd <= now ? "Expired" : item.Status,
                database.PlanPrices.Where(price => price.Id == item.CurrentPlanPriceId).Select(price => price.Period).Single(),
                item.CurrentPeriodStart, item.CurrentPeriodEnd, item.CancelAtPeriodEnd)).ToArrayAsync(cancellationToken);
        return new(count, page, 20, rows);
    }

    public async Task<AdministrationPage<BillingAuditItem>> AuditAsync(DateTimeOffset? from, DateTimeOffset? to, int page, CancellationToken cancellationToken)
    {
        var query = database.AuditLogs.AsNoTracking().Where(item => item.EntityType == "TestCard" || item.EntityType == "CardTransaction"
            || item.EntityType == "PaymentAttempt" || item.EntityType == "BillingOrder" || item.EntityType == "Subscription"
            || item.EntityType == "Refund" || item.EntityType == "BillingJobExecution" || item.EntityType == "BillingPermission"
            || item.EntityType == "HeartRefillPurchase" || item.EntityType == "PlanPrice" || item.EntityType == "SubscriptionPlan");
        if (from is { } start) query = query.Where(item => item.OccurredAt >= start.ToUniversalTime());
        if (to is { } end) query = query.Where(item => item.OccurredAt < end.ToUniversalTime());
        int count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).Skip((page - 1) * 20).Take(20)
            .Select(item => new BillingAuditItem(item.Id, item.ActorId, item.Action, item.EntityType, item.EntityId, item.Reason, item.TraceId, item.OccurredAt)).ToArrayAsync(cancellationToken);
        return new(count, page, 20, rows);
    }
}
