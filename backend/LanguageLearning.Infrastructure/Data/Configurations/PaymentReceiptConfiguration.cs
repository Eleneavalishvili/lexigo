using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class PaymentReceiptConfiguration : IEntityTypeConfiguration<PaymentReceipt>
{
    public void Configure(EntityTypeBuilder<PaymentReceipt> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasOne<PaymentAttempt>().WithMany().HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.ReceiptNumber).HasMaxLength(50);
        builder.HasIndex(item => item.PaymentId).IsUnique();
        builder.HasIndex(item => item.ReceiptNumber).IsUnique();
    }
}
