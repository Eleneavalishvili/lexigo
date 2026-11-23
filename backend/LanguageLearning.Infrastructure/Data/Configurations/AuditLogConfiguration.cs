using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Action).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.TraceId).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.Reason).HasMaxLength(2000);
        builder.HasIndex(entry => entry.OccurredAt);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(entry => entry.ActorId).OnDelete(DeleteBehavior.SetNull);
    }
}
