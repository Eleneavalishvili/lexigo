using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ExerciseOptionConfiguration : IEntityTypeConfiguration<ExerciseOption>
{
    public void Configure(EntityTypeBuilder<ExerciseOption> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Text).HasMaxLength(2000);
        builder.Property(item => item.MatchKey).HasMaxLength(200);
        builder.HasIndex(item => new { item.ExerciseId, item.OrderNumber }).IsUnique();
        builder.HasOne<Exercise>().WithMany().HasForeignKey(item => item.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(item => item.AudioMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("ExerciseOptions", table => table.HasCheckConstraint("CK_ExerciseOptions_Order", "\"OrderNumber\" > 0"));
    }
}

