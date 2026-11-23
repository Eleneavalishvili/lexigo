using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class SubscriptionQueries(LexiGoDbContext database, TimeProvider clock, IOptions<BillingRenewalOptions> renewalOptions)
{
    public async Task<PlanView[]> PlansAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var plans = await database.SubscriptionPlans.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code).ToArrayAsync(cancellationToken);
        var prices = await database.PlanPrices.AsNoTracking().Where(item => item.ValidFrom <= now && (item.ValidTo == null || item.ValidTo > now))
            .OrderBy(item => item.Amount).ToArrayAsync(cancellationToken);
        var features = await FeaturesAsync(cancellationToken);
        return plans.Select(plan => new PlanView(plan.Id, plan.Code, plan.Name,
            prices.Where(price => price.PlanId == plan.Id).Select(price => new PlanPriceView(price.Id, price.Period, price.Amount, price.Currency)).ToArray(),
            features.Where(item => item.PlanId == plan.Id).Select(item => item.View).ToArray())).ToArray();
    }

    public async Task<bool> HasAsync(Guid userId, string featureCode, CancellationToken cancellationToken)
    {
        if (!BillingCatalogSeed.Available(featureCode)) return false;
        var now = clock.GetUtcNow();
        Guid planId = await database.Subscriptions.Where(item => item.UserId == userId && item.Status == "Active"
            && item.CurrentPeriodStart <= now && now < item.CurrentPeriodEnd).Select(item => (Guid?)item.PlanId).SingleOrDefaultAsync(cancellationToken) ?? BillingCatalogSeed.Free;
        return await (from link in database.PlanFeatures
                      join feature in database.Features on link.FeatureId equals feature.Id
                      where link.PlanId == planId && feature.Code == featureCode
                      select feature.Id).AnyAsync(cancellationToken);
    }

    public async Task<EntitlementView> EntitlementsAsync(Guid userId, CancellationToken cancellationToken)
    {
        Guid plan = await CurrentAsync(userId, cancellationToken) is { } subscription ? subscription.PlanId : BillingCatalogSeed.Free;
        var code = await database.SubscriptionPlans.Where(item => item.Id == plan).Select(item => item.Code).SingleAsync(cancellationToken);
        var features = await FeaturesAsync(cancellationToken);
        return new(code, features.Where(item => item.PlanId == plan).Select(item => item.View).ToArray());
    }

    public Task<Subscription?> CurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return database.Subscriptions.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.Status == "Active"
            && item.CurrentPeriodStart <= now && now < item.CurrentPeriodEnd, cancellationToken);
    }

    public async Task<SubscriptionView> SubscriptionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var current = await CurrentAsync(userId, cancellationToken);
        var preference = await database.BillingPreferences.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (current is null)
        {
            var latestId = await database.Subscriptions.AsNoTracking().Where(item => item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
            return new(null, "FREE", "Free", null, null, null, false, preference?.AutoRenew ?? false, preference?.DefaultTestCardId, null,
                RenewalProcessingAvailable: renewalOptions.Value.Enabled, RenewalConsentRequired: preference?.AutoRenew == true,
                RenewalTargetSubscriptionId: preference?.RenewalConsentSubscriptionId ?? latestId);
        }
        string code = await database.SubscriptionPlans.Where(item => item.Id == current.PlanId).Select(item => item.Code).SingleAsync(cancellationToken);
        string period = await database.PlanPrices.Where(item => item.Id == current.CurrentPlanPriceId).Select(item => item.Period).SingleAsync(cancellationToken);
        var scheduled = current.ScheduledPlanPriceId is { } scheduledId
            ? await database.PlanPrices.AsNoTracking().Where(item => item.Id == scheduledId)
                .Select(item => new PlanPriceView(item.Id, item.Period, item.Amount, item.Currency)).SingleOrDefaultAsync(cancellationToken) : null;
        return new(current.Id, code, current.Status, period, current.CurrentPeriodStart, current.CurrentPeriodEnd,
            current.CancelAtPeriodEnd, preference?.AutoRenew ?? false, preference?.DefaultTestCardId, current.ScheduledPlanPriceId, scheduled,
            renewalOptions.Value.Enabled, preference?.AutoRenew == true && (preference.RenewalConsentAt is null || preference.RenewalConsentSubscriptionId != current.Id), current.Id);
    }

    public async Task<BillingOrderView?> OrderAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var order = await database.BillingOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        return order is null ? null : View(order, clock.GetUtcNow());
    }

    public async Task<BillingOrderView?> OrderByRequestAsync(Guid userId, Guid key, CancellationToken cancellationToken)
    {
        Guid? id = await database.BillingOrders.Where(item => item.UserId == userId && item.IdempotencyKey == key).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        return id is null ? null : await OrderAsync(userId, id.Value, cancellationToken);
    }

    public async Task<PaymentView?> PaymentByRequestAsync(Guid userId, Guid key, CancellationToken cancellationToken)
    {
        Guid? id = await database.PaymentAttempts.Where(item => item.UserId == userId && item.IdempotencyKey == key).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        return id is null ? null : await PaymentAsync(userId, id.Value, cancellationToken);
    }

    public async Task<PaymentView?> PaymentAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var payment = await database.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (payment is null) return null;
        var order = await database.BillingOrders.AsNoTracking().SingleAsync(item => item.Id == payment.OrderId, cancellationToken);
        string? receipt = await database.PaymentReceipts.Where(item => item.PaymentId == id).Select(item => item.ReceiptNumber).SingleOrDefaultAsync(cancellationToken);
        return View(payment, order.SubscriptionId, receipt);
    }

    public static BillingOrderView View(BillingOrder order, DateTimeOffset now) => new(order.Id, order.PlanPriceId, order.AmountSnapshot, order.Currency,
        order.PeriodSnapshot, order.Status == "Created" && order.ExpiresAt <= now ? "Expired" : order.Status, order.CreatedAt, order.ExpiresAt, order.SubscriptionId);
    public static PaymentView View(PaymentAttempt payment, Guid? subscriptionId, string? receipt) => new(payment.Id, payment.OrderId,
        payment.TestCardId, payment.Status, payment.FailureCode, payment.Amount, payment.Currency, payment.CreatedAt, payment.CompletedAt,
        payment.Status == "Succeeded" ? subscriptionId : null, receipt);

    private async Task<(Guid PlanId, PlanFeatureView View)[]> FeaturesAsync(CancellationToken cancellationToken)
    {
        var rows = await (from link in database.PlanFeatures.AsNoTracking()
                          join feature in database.Features on link.FeatureId equals feature.Id
                          orderby feature.Id
                          select new { link.PlanId, feature.Code, feature.Description, link.LimitValue }).ToArrayAsync(cancellationToken);
        return rows.Select(item => (item.PlanId, new PlanFeatureView(item.Code, item.Description, item.LimitValue, BillingCatalogSeed.Available(item.Code)))).ToArray();
    }
}
