using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.Code).IsUnique();
        builder.Property(item => item.Code).HasMaxLength(24);
        builder.Property(item => item.Name).HasMaxLength(80);
        builder.HasData(
            new SubscriptionPlan { Id = BillingCatalogSeed.Free, Code = "FREE", Name = "Free", IsActive = true, CreatedAt = BillingCatalogSeed.EffectiveFrom },
            new SubscriptionPlan { Id = BillingCatalogSeed.Premium, Code = "PREMIUM", Name = "Premium", IsActive = true, CreatedAt = BillingCatalogSeed.EffectiveFrom });
    }
}
