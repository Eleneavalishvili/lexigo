using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class BillingOperationConfiguration : IEntityTypeConfiguration<BillingOperation>
{
    public void Configure(EntityTypeBuilder<BillingOperation> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Operation).HasMaxLength(30);
        builder.Property(item => item.RequestSnapshot).HasColumnType("jsonb");
        builder.Property(item => item.ResponseSnapshot).HasColumnType("jsonb");
        builder.HasIndex(item => new { item.UserId, item.Operation, item.IdempotencyKey }).IsUnique();
    }
}
