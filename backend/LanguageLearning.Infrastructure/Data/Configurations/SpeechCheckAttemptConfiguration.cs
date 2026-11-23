using LanguageLearning.Core.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class SpeechCheckAttemptConfiguration : IEntityTypeConfiguration<SpeechCheckAttempt>
{
    public void Configure(EntityTypeBuilder<SpeechCheckAttempt> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.UserId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.UserId, item.SessionExerciseId, item.Status });
        builder.HasIndex(item => new { item.UserId, item.CreatedAt });
        builder.Property(item => item.AudioSha256).HasMaxLength(64);
        builder.Property(item => item.Status).HasMaxLength(20);
        builder.Property(item => item.ResponseSnapshot).HasColumnType("jsonb");
        builder.HasOne<SessionExercise>().WithMany().HasForeignKey(item => new { item.UserId, item.SessionExerciseId })
            .HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_SpeechCheckAttempt_Values",
            "\"Status\" IN ('Matched','Mismatch','NoSpeech') AND \"HeartsCharged\" BETWEEN 0 AND 1 AND (\"HeartsCharged\" = 0 OR \"Status\" = 'Mismatch') AND length(\"AudioSha256\") = 64"));
    }
}
