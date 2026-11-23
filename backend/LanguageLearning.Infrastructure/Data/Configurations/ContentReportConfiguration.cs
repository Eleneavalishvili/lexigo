using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        builder.Property(item => item.ResourceType).HasMaxLength(20);
        builder.Property(item => item.Reason).HasMaxLength(20);
        builder.Property(item => item.Comment).HasMaxLength(1000);
        builder.Property(item => item.Status).HasMaxLength(20);
        builder.Property(item => item.Resolution).HasMaxLength(1000);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReporterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ResolvedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LessonVersion>().WithMany().HasForeignKey(item => item.VersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ReporterId, item.CreatedAt, item.Id });
        builder.HasIndex(item => new { item.ReporterId, item.ResourceId, item.VersionId, item.Reason }).IsUnique();
        builder.HasIndex(item => new { item.Status, item.CreatedAt, item.Id });
        builder.ToTable("ContentReports", table => table.HasCheckConstraint("CK_ContentReports_Status",
            "\"Status\" IN ('New', 'InReview', 'Resolved', 'Rejected')"));
    }
}
