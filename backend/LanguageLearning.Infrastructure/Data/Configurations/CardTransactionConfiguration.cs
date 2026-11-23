using LanguageLearning.Core.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class CardTransactionConfiguration : IEntityTypeConfiguration<CardTransaction>
{
    public void Configure(EntityTypeBuilder<CardTransaction> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasOne<TestCard>().WithMany().HasForeignKey(item => item.TestCardId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Type).HasMaxLength(20);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.BalanceAfter).HasPrecision(18, 2);
        builder.Property(item => item.Reference).HasMaxLength(50);
        builder.HasIndex(item => item.Reference).IsUnique();
        builder.HasIndex(item => new { item.TestCardId, item.CreatedAt });
        builder.HasOne<HeartRefillPurchase>().WithMany()
            .HasForeignKey(item => new { item.TestCardId, item.HeartRefillPurchaseId })
            .HasPrincipalKey(item => new { item.TestCardId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.HeartRefillPurchaseId).IsUnique();
        builder.HasOne<PaymentAttempt>().WithMany().HasForeignKey(item => new { item.TestCardId, item.PaymentId })
            .HasPrincipalKey(item => new { item.TestCardId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.PaymentId).IsUnique();
        builder.HasOne<Refund>().WithMany().HasForeignKey(item => item.RefundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.RefundId).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("CK_CardTransaction_Values",
            "\"Amount\" > 0 AND \"BalanceAfter\" >= 0 AND ((\"Type\" = 'TopUp' AND \"Amount\" <= 1000 AND \"BalanceAfter\" <= 10000 AND \"RefundId\" IS NULL AND \"HeartRefillPurchaseId\" IS NULL AND \"PaymentId\" IS NULL) OR (\"Type\" = 'Debit' AND \"Amount\" <= 10000 AND \"RefundId\" IS NULL AND ((\"HeartRefillPurchaseId\" IS NOT NULL AND \"PaymentId\" IS NULL) OR (\"PaymentId\" IS NOT NULL AND \"HeartRefillPurchaseId\" IS NULL))) OR (\"Type\" = 'Refund' AND \"RefundId\" IS NOT NULL AND \"HeartRefillPurchaseId\" IS NULL AND \"PaymentId\" IS NULL))"));
    }
}
