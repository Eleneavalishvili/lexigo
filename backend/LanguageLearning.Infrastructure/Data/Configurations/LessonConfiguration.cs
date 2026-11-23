using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(160);
        builder.Property(item => item.Objective).HasMaxLength(1000);
        builder.Property(item => item.Status).HasMaxLength(20);
        builder.HasIndex(item => new { item.UnitId, item.OrderNumber }).IsUnique();
        builder.HasOne<Unit>().WithMany().HasForeignKey(item => item.UnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LessonVersion>().WithMany().HasForeignKey(item => new { item.Id, item.CurrentVersionId }).HasPrincipalKey(item => new { item.LessonId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("Lessons", table =>
        {
            table.HasCheckConstraint("CK_Lessons_Order", "\"OrderNumber\" > 0");
            table.HasCheckConstraint("CK_Lessons_Status", "\"Status\" IN ('Draft','Submitted','Approved','Published','Archived')");
        });
    }
}

