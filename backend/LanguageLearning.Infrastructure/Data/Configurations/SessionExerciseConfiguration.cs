using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class SessionExerciseConfiguration : IEntityTypeConfiguration<SessionExercise>
{
    public void Configure(EntityTypeBuilder<SessionExercise> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.UserId, item.Id });
        builder.Property(item => item.PublicSnapshot).HasColumnType("jsonb");
        builder.Property(item => item.PrivateEvaluationSnapshot).HasColumnType("jsonb");
        builder.HasIndex(item => new { item.SessionId, item.OrderNumber }).IsUnique();
        builder.HasIndex(item => new { item.SessionId, item.ExerciseId }).IsUnique();
        builder.HasOne<LearningSession>().WithMany().HasForeignKey(item => new { item.UserId, item.SessionId }).HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(item => item.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LanguageLearning.Core.Content.MediaAsset>().WithMany().HasForeignKey(item => item.AudioMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("SessionExercises", table => table.HasCheckConstraint("CK_SessionExercises_Order", "\"OrderNumber\" > 0"));
    }
}
