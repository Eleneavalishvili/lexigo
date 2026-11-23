using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class LessonPrerequisiteConfiguration : IEntityTypeConfiguration<LessonPrerequisite>
{
    public void Configure(EntityTypeBuilder<LessonPrerequisite> builder)
    {
        builder.HasKey(item => new { item.LessonId, item.PrerequisiteLessonId });
        builder.HasOne<Lesson>().WithMany().HasForeignKey(item => item.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Lesson>().WithMany().HasForeignKey(item => item.PrerequisiteLessonId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("LessonPrerequisites", table => table.HasCheckConstraint("CK_Prerequisites_NotSelf", "\"LessonId\" <> \"PrerequisiteLessonId\""));
    }
}

