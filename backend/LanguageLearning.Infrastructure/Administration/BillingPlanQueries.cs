using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed record ManagedPlanPrice(Guid Id, string Period, decimal Amount, string Currency,
    DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);
public sealed record ManagedPlanFeature(int FeatureId, string Code, string Description, int? LimitValue, bool Available);
public sealed record ManagedPlan(Guid Id, string Code, string Name, bool IsActive, DateTimeOffset CreatedAt,
    ManagedPlanPrice[] Prices, ManagedPlanFeature[] Features);

public sealed class BillingPlanQueries(LexiGoDbContext database)
{
    public async Task<ManagedPlanFeature[]> FeaturesAsync(CancellationToken cancellationToken)
    {
        var features = await database.Features.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(cancellationToken);
        return features.Select(item => new ManagedPlanFeature(item.Id, item.Code, item.Description, null,
            BillingCatalogSeed.Available(item.Code))).ToArray();
    }

    public async Task<AdministrationPage<ManagedPlan>> ReadAsync(bool? active, int page, CancellationToken cancellationToken)
    {
        // Keep the plan, price and feature projections on one consistent catalogue snapshot.
        await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var query = database.SubscriptionPlans.AsNoTracking().AsQueryable();
        if (active is { } enabled) query = query.Where(item => item.IsActive == enabled);
        int count = await query.CountAsync(cancellationToken);
        var plans = await query.OrderBy(item => item.Code).ThenBy(item => item.Id)
            .Skip((page - 1) * 20).Take(20).ToArrayAsync(cancellationToken);
        var ids = plans.Select(item => item.Id).ToArray();
        var prices = await database.PlanPrices.AsNoTracking().Where(item => ids.Contains(item.PlanId))
            .OrderBy(item => item.Period).ThenBy(item => item.Currency).ThenByDescending(item => item.ValidFrom)
            .ToArrayAsync(cancellationToken);
        var features = await (from link in database.PlanFeatures.AsNoTracking()
                              join feature in database.Features on link.FeatureId equals feature.Id
                              where ids.Contains(link.PlanId)
                              orderby feature.Id
                              select new { link.PlanId, feature.Id, feature.Code, feature.Description, link.LimitValue })
            .ToArrayAsync(cancellationToken);
        var rows = plans.Select(plan => new ManagedPlan(plan.Id, plan.Code, plan.Name, plan.IsActive, plan.CreatedAt,
            prices.Where(item => item.PlanId == plan.Id).Select(item => new ManagedPlanPrice(item.Id, item.Period,
                item.Amount, item.Currency, item.ValidFrom, item.ValidTo)).ToArray(),
            features.Where(item => item.PlanId == plan.Id).Select(item => new ManagedPlanFeature(item.Id, item.Code,
                item.Description, item.LimitValue, BillingCatalogSeed.Available(item.Code))).ToArray())).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new(count, page, 20, rows);
    }
}
