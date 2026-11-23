using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.HasKey(item => new { item.PlanId, item.FeatureId });
        builder.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Feature>().WithMany().HasForeignKey(item => item.FeatureId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_PlanFeature_Limit", "\"LimitValue\" IS NULL OR \"LimitValue\" >= 0"));
        builder.HasData(BillingCatalogSeed.Features.Select(item => new PlanFeature { PlanId = BillingCatalogSeed.Premium, FeatureId = item.Id })
            .Concat(BillingCatalogSeed.Features.Take(5).Select(item => new PlanFeature { PlanId = BillingCatalogSeed.Free, FeatureId = item.Id })));
    }
}
