using LanguageLearning.Core.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class BillingJobExecutionConfiguration : IEntityTypeConfiguration<BillingJobExecution>
{
    public void Configure(EntityTypeBuilder<BillingJobExecution> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasOne<Subscription>().WithMany().HasForeignKey(item => item.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.SubscriptionId, item.PeriodKey }).IsUnique();
        builder.Property(item => item.PeriodKey).HasMaxLength(50);
        builder.Property(item => item.Status).HasMaxLength(12);
        builder.Property(item => item.FailureCode).HasMaxLength(40);
        builder.ToTable(table => table.HasCheckConstraint("CK_BillingJobExecution_Result",
            "\"NextAttemptAt\" IS NULL AND ((\"Status\" = 'Succeeded' AND \"FailureCode\" IS NULL) OR (\"Status\" IN ('Failed','Skipped') AND \"FailureCode\" IS NOT NULL))"));
    }
}
