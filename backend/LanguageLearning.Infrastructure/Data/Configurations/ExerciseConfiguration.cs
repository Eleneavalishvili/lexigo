using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Type).HasMaxLength(40);
        builder.Property(item => item.Prompt).HasMaxLength(5000);
        builder.Property(item => item.ContextText).HasMaxLength(4000);
        builder.Property(item => item.Explanation).HasMaxLength(5000);
        builder.Property(item => item.EvaluationRules).HasColumnType("jsonb");
        builder.HasIndex(item => new { item.LessonVersionId, item.OrderNumber }).IsUnique();
        builder.HasOne<LessonVersion>().WithMany().HasForeignKey(item => item.LessonVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(item => item.AudioMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("Exercises", table => table.HasCheckConstraint("CK_Exercises_Order", "\"OrderNumber\" > 0"));
    }
}

