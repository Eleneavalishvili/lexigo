using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class SkillConfiguration :
    IEntityTypeConfiguration<Skill>,
    IEntityTypeConfiguration<ExerciseSkill>,
    IEntityTypeConfiguration<SkillProgress>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(100);
        builder.Property(item => item.Category).HasMaxLength(40);
        builder.Property(item => item.Revision).HasDefaultValue(1);
        builder.ToTable(table => table.HasCheckConstraint("CK_Skill_Revision", "\"Revision\" > 0"));
        builder.HasIndex(item => new { item.CourseId, item.Name }).IsUnique();
        builder.HasOne<Course>().WithMany().HasForeignKey(item => item.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ExerciseSkill> builder)
    {
        builder.HasKey(item => new { item.ExerciseId, item.SkillId });
        builder.HasOne<Exercise>().WithMany().HasForeignKey(item => item.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Skill>().WithMany().HasForeignKey(item => item.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SkillProgress> builder)
    {
        builder.HasKey(item => new { item.UserId, item.SkillId });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Skill>().WithMany().HasForeignKey(item => item.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("SkillProgress", table => table.HasCheckConstraint("CK_SkillProgress_Counts",
            """
            "CorrectCount" >= 0 AND "IncorrectCount" >= 0
            AND (("CorrectCount" = 0 AND "IncorrectCount" = 0) = ("LastPracticedAt" IS NULL))
            """));
    }
}
