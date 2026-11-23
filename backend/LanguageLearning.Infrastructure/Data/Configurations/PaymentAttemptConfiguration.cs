using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TestCardId, item.Id });
        builder.HasOne<BillingOrder>().WithMany().HasForeignKey(item => new { item.UserId, item.OrderId })
            .HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TestCard>().WithMany().HasForeignKey(item => new { item.UserId, item.TestCardId })
            .HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UserId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => item.OrderId).IsUnique().HasFilter("\"Status\" = 'Succeeded'");
        builder.Property(item => item.Status).HasMaxLength(12);
        builder.Property(item => item.FailureCode).HasMaxLength(40);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.ToTable(table => table.HasCheckConstraint("CK_PaymentAttempt_Values", "\"Amount\" > 0 AND \"Currency\" IN ('GEL','USD','EUR') AND ((\"Status\" = 'Pending' AND \"CompletedAt\" IS NULL AND \"FailureCode\" IS NULL) OR (\"Status\" = 'Succeeded' AND \"CompletedAt\" IS NOT NULL AND \"FailureCode\" IS NULL) OR (\"Status\" = 'Failed' AND \"CompletedAt\" IS NOT NULL AND \"FailureCode\" IS NOT NULL AND \"FailureCode\" IN ('CardBlocked','CurrencyMismatch','InsufficientBalance','TechnicalFailure','AlreadySubscribed','OrderExpired')))"));
    }
}
