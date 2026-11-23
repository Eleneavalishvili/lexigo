using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Type).HasMaxLength(50);
        builder.Property(item => item.Title).HasMaxLength(160);
        builder.Property(item => item.Body).HasMaxLength(2000);
        builder.HasIndex(item => new { item.UserId, item.CreatedAt, item.Id });
        builder.HasOne<Assignment>().WithMany().HasForeignKey(item => item.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.AssignmentId, item.UserId, item.Type }).IsUnique()
            .HasFilter("\"AssignmentId\" IS NOT NULL");
        builder.ToTable(table => table.HasCheckConstraint("CK_Notification_ReadTime", "\"ReadAt\" IS NULL OR \"ReadAt\" >= \"CreatedAt\""));
    }
}
