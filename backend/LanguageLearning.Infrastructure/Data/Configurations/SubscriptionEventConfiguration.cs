using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class SubscriptionEventConfiguration : IEntityTypeConfiguration<SubscriptionEvent>
{
    public void Configure(EntityTypeBuilder<SubscriptionEvent> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasOne<Refund>().WithMany().HasForeignKey(item => item.RefundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.RefundId).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("CK_SubscriptionEvent_Refund", "\"RefundId\" IS NULL OR \"RelatedPaymentId\" IS NOT NULL"));
        builder.HasOne<Subscription>().WithMany().HasForeignKey(item => item.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentAttempt>().WithMany().HasForeignKey(item => item.RelatedPaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.EventType).HasMaxLength(50);
        builder.HasIndex(item => new { item.SubscriptionId, item.OccurredAt });
    }
}
