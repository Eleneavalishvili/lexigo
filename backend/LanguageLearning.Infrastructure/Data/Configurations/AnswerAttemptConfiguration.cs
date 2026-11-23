using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class AnswerAttemptConfiguration : IEntityTypeConfiguration<AnswerAttempt>
{
    public void Configure(EntityTypeBuilder<AnswerAttempt> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AnswerPayload).HasColumnType("jsonb");
        builder.Property(item => item.ResponseSnapshot).HasColumnType("jsonb");
        builder.Property(item => item.IsGraded).HasDefaultValue(true).HasSentinel(true);
        builder.ToTable("AnswerAttempts", table => table.HasCheckConstraint("CK_AnswerAttempts_Ungraded", "\"IsGraded\" OR NOT \"IsCorrect\""));
        builder.HasIndex(item => new { item.UserId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => item.SessionExerciseId).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionExercise>().WithMany().HasForeignKey(item => new { item.UserId, item.SessionExerciseId }).HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
