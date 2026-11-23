using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Code).HasMaxLength(50);
        builder.Property(item => item.Description).HasMaxLength(200);
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasData(BillingCatalogSeed.Features);
    }
}
