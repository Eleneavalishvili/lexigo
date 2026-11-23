using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

internal static class PlanDraftFeatures
{
    internal static bool Valid(PlanFeatureEdit[]? features, PlanFeatureEdit[]? expected) =>
        features is null ? expected is null : ValidList(features) && expected is not null && ValidList(expected);

    private static bool ValidList(PlanFeatureEdit[] features) =>
        features.Length <= 50 && features.All(item => item is not null && item.FeatureId > 0 && item.LimitValue is not <= 0)
        && features.Select(item => item.FeatureId).Distinct().Count() == features.Length;

    internal static async Task<string?> ApplyAsync(LexiGoDbContext database, SubscriptionPlan plan,
        PlanFeatureEdit[] features, PlanFeatureEdit[] expected, CancellationToken cancellationToken)
    {
        if (plan.IsActive || plan.Id == BillingCatalogSeed.Free || plan.Id == BillingCatalogSeed.Premium ||
            await database.PlanPrices.AnyAsync(item => item.PlanId == plan.Id, cancellationToken) ||
            await database.Subscriptions.AnyAsync(item => item.PlanId == plan.Id, cancellationToken))
            return "Only inactive drafts without purchase history or prices can change features.";

        var current = await database.PlanFeatures.Where(item => item.PlanId == plan.Id)
            .OrderBy(item => item.FeatureId).ToArrayAsync(cancellationToken);
        if (!current.Select(item => new PlanFeatureEdit(item.FeatureId, item.LimitValue))
                .SequenceEqual(expected.OrderBy(item => item.FeatureId)))
            return "Reload the catalogue before changing these features.";

        var desired = features.ToDictionary(item => item.FeatureId);
        var ids = desired.Keys.ToArray();
        if (await database.Features.CountAsync(item => ids.Contains(item.Id), cancellationToken) != ids.Length)
            return "One or more selected features are no longer in the catalogue.";

        foreach (var link in current)
        {
            if (desired.Remove(link.FeatureId, out var update)) link.LimitValue = update.LimitValue;
            else database.PlanFeatures.Remove(link);
        }
        database.PlanFeatures.AddRange(desired.Values.Select(item =>
            new PlanFeature { PlanId = plan.Id, FeatureId = item.FeatureId, LimitValue = item.LimitValue }));
        return null;
    }
}
