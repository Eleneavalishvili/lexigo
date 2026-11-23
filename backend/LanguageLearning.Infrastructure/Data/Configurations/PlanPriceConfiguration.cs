using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class PlanPriceConfiguration : IEntityTypeConfiguration<PlanPrice>
{
    public void Configure(EntityTypeBuilder<PlanPrice> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.PlanId, item.Id });
        builder.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Period).HasMaxLength(12);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.HasIndex(item => new { item.PlanId, item.Period, item.Currency, item.ValidFrom }).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("CK_PlanPrice_Values", "\"Period\" IN ('Monthly','Annual') AND \"Currency\" IN ('GEL','USD','EUR') AND \"Amount\" > 0 AND \"Amount\" <= 10000 AND (\"ValidTo\" IS NULL OR \"ValidTo\" > \"ValidFrom\")"));
        builder.HasData(
            new PlanPrice { Id = BillingCatalogSeed.Monthly, PlanId = BillingCatalogSeed.Premium, Period = "Monthly", Amount = 9.99m, Currency = "GEL", ValidFrom = BillingCatalogSeed.EffectiveFrom },
            new PlanPrice { Id = BillingCatalogSeed.Annual, PlanId = BillingCatalogSeed.Premium, Period = "Annual", Amount = 79.99m, Currency = "GEL", ValidFrom = BillingCatalogSeed.EffectiveFrom });
    }
}
