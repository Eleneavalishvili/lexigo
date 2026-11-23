using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasOne<PaymentAttempt>().WithMany().HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.CreatedBy, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => item.PaymentId);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.Property(item => item.ReasonFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(12);
        builder.ToTable(table => table.HasCheckConstraint("CK_Refund_Result", "\"Amount\" > 0 AND \"Amount\" <= 10000 AND length(btrim(\"Reason\")) BETWEEN 3 AND 500 AND \"Status\" = 'Succeeded' AND \"CompletedAt\" IS NOT NULL AND \"CompletedAt\" >= \"CreatedAt\""));
    }
}
