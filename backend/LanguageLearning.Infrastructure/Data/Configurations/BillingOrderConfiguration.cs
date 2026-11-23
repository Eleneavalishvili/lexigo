using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class BillingOrderConfiguration : IEntityTypeConfiguration<BillingOrder>
{
    public void Configure(EntityTypeBuilder<BillingOrder> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.UserId, item.Id });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanPrice>().WithMany().HasForeignKey(item => item.PlanPriceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subscription>().WithMany().HasForeignKey(item => new { item.UserId, item.SubscriptionId })
            .HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UserId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.SubscriptionId, item.RenewalPeriodKey }).IsUnique().HasFilter("\"RenewalPeriodKey\" IS NOT NULL");
        builder.Property(item => item.AmountSnapshot).HasPrecision(18, 2);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.Property(item => item.PeriodSnapshot).HasMaxLength(12);
        builder.Property(item => item.Status).HasMaxLength(12);
        builder.Property(item => item.OrderType).HasMaxLength(12);
        builder.Property(item => item.RenewalPeriodKey).HasMaxLength(50);
        builder.ToTable(table => table.HasCheckConstraint("CK_BillingOrder_Values", "\"AmountSnapshot\" > 0 AND \"AmountSnapshot\" <= 10000 AND \"Currency\" IN ('GEL','USD','EUR') AND \"PeriodSnapshot\" IN ('Monthly','Annual') AND \"Status\" IN ('Created','Paid','Cancelled','Expired') AND \"OrderType\" IN ('Purchase','Renewal') AND \"ExpiresAt\" > \"CreatedAt\" AND (\"Status\" <> 'Paid' OR \"SubscriptionId\" IS NOT NULL)"));
    }
}
