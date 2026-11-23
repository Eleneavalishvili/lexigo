using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.UserId, item.Id });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanPrice>().WithMany().HasForeignKey(item => new { item.PlanId, item.CurrentPlanPriceId })
            .HasPrincipalKey(item => new { item.PlanId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanPrice>().WithMany().HasForeignKey(item => item.ScheduledPlanPriceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.UserId).IsUnique().HasFilter("\"Status\" = 'Active'");
        builder.Property(item => item.Status).HasMaxLength(12);
        builder.ToTable(table => table.HasCheckConstraint("CK_Subscription_Values", "\"Status\" IN ('Active','Expired','Refunded') AND \"CurrentPeriodEnd\" > \"CurrentPeriodStart\""));
    }
}
