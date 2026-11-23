using LanguageLearning.Core.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class HeartRefillPurchaseConfiguration : IEntityTypeConfiguration<HeartRefillPurchase>
{
    public void Configure(EntityTypeBuilder<HeartRefillPurchase> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TestCardId, item.Id });
        builder.HasOne<TestCard>().WithMany().HasForeignKey(item => new { item.UserId, item.TestCardId })
            .HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UserId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.UserId, item.CreatedAt });
        builder.HasIndex(item => item.Reference).IsUnique();
        builder.Property(item => item.OfferCode).HasMaxLength(50);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.Property(item => item.Status).HasMaxLength(12);
        builder.Property(item => item.FailureCode).HasMaxLength(30);
        builder.Property(item => item.Reference).HasMaxLength(50);
        builder.ToTable(table => table.HasCheckConstraint("CK_HeartRefillPurchase_Values",
            "\"Amount\" > 0 AND \"Amount\" <= 1000 AND \"Currency\" = 'GEL' AND \"HeartsBefore\" BETWEEN 0 AND 5 AND \"HeartsAfter\" BETWEEN 0 AND 5 AND ((\"Status\" = 'Succeeded' AND \"FailureCode\" IS NULL AND \"HeartsBefore\" < 5 AND \"HeartsAfter\" = 5) OR (\"Status\" = 'Failed' AND \"FailureCode\" IS NOT NULL AND \"FailureCode\" IN ('CardBlocked','CurrencyMismatch','InsufficientBalance','HeartsAlreadyFull') AND \"HeartsBefore\" = \"HeartsAfter\"))"));
    }
}
