using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> builder)
    {
        builder.HasKey(item => new { item.UserId, item.LessonId });
        builder.Property(item => item.Status).HasMaxLength(20);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Lesson>().WithMany().HasForeignKey(item => item.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("LessonProgress", table =>
        {
            table.HasCheckConstraint("CK_LessonProgress_Score", "\"BestScore\" BETWEEN 0 AND 100");
            table.HasCheckConstraint("CK_LessonProgress_Status", "\"Status\" IN ('InProgress','Completed')");
        });
    }
}
