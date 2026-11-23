using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class LessonVersionConfiguration : IEntityTypeConfiguration<LessonVersion>
{
    public void Configure(EntityTypeBuilder<LessonVersion> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.LessonId, item.Id });
        builder.HasIndex(item => new { item.LessonId, item.VersionNumber }).IsUnique();
        builder.Property(item => item.Title).HasMaxLength(160);
        builder.Property(item => item.Objective).HasMaxLength(1000);
        builder.Property(item => item.Explanation).HasMaxLength(20000);
        builder.Property(item => item.Status).HasMaxLength(20);
        builder.HasOne<Lesson>().WithMany().HasForeignKey(item => item.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.LessonId).IsUnique().HasFilter("\"Status\" IN ('Draft','Submitted','Approved')");
        builder.ToTable("LessonVersions", table =>
        {
            table.HasCheckConstraint("CK_LessonVersions_Number", "\"VersionNumber\" > 0");
            table.HasCheckConstraint("CK_LessonVersions_Status", "\"Status\" IN ('Draft','Submitted','Approved','Published','Archived')");
            table.HasCheckConstraint("CK_LessonVersions_PrivateErasure",
                "\"PrivateTextErasedAt\" IS NULL OR (\"Status\" = 'Archived' AND \"PublishedAt\" IS NULL)");
        });
    }
}

