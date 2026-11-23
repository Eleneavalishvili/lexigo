using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ReminderDeliveryConfiguration : IEntityTypeConfiguration<ReminderDelivery>
{
    public void Configure(EntityTypeBuilder<ReminderDelivery> builder)
    {
        builder.HasKey(delivery => delivery.Id);
        builder.HasIndex(delivery => new { delivery.UserId, delivery.LocalDate, delivery.Type }).IsUnique();
        builder.Property(delivery => delivery.Type).HasMaxLength(32);
        builder.Property(delivery => delivery.Status).HasMaxLength(16);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(delivery => delivery.UserId);
        builder.HasOne<EmailOutboxMessage>().WithOne().HasForeignKey<ReminderDelivery>(delivery => delivery.Id);
        builder.ToTable("ReminderDeliveries", table => table.HasCheckConstraint(
            "CK_ReminderDeliveries_Status",
            """("Status" = 'Sent' AND "SentAt" IS NOT NULL) OR ("Status" IN ('Queued', 'Cancelled', 'Failed') AND "SentAt" IS NULL)"""));
    }
}
