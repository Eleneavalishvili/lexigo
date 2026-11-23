using LanguageLearning.Core.Content;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class CompletionCardConfiguration : IEntityTypeConfiguration<CompletionCard>
{
    public void Configure(EntityTypeBuilder<CompletionCard> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.UserId, item.UnitId }).IsUnique();
        builder.HasIndex(item => item.VerificationCode).IsUnique();
        builder.HasIndex(item => item.ShareCode).IsUnique();
        builder.Property(item => item.VerificationCode).HasMaxLength(32);
        builder.Property(item => item.ShareCode).HasMaxLength(32);
        builder.Property(item => item.CourseTitle).HasMaxLength(120);
        builder.Property(item => item.UnitTitle).HasMaxLength(160);
        builder.Property(item => item.AverageBestScore).HasPrecision(5, 2);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(item => new { item.UserId, item.CourseId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Unit>().WithMany().HasForeignKey(item => new { item.CourseId, item.UnitId })
            .HasPrincipalKey(item => new { item.CourseId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("CompletionCards", table =>
        {
            table.HasCheckConstraint("CK_CompletionCards_Values", """
                "LessonCount" > 0 AND "GradedLessonCount" BETWEEN 0 AND "LessonCount" AND "Revision" > 0
                AND "CompletedAt" <= "IssuedAt"
                AND (("GradedLessonCount" = 0 AND "AverageBestScore" IS NULL)
                    OR ("GradedLessonCount" > 0 AND "AverageBestScore" IS NOT NULL AND "AverageBestScore" BETWEEN 0 AND 100))
                """);
            table.HasCheckConstraint("CK_CompletionCards_Sharing", """
                "VerificationCode" ~ '^[0-9a-f]{32}$'
                AND ((NOT "IsShared" AND "ShareCode" IS NULL)
                    OR ("IsShared" AND "ShareCode" ~ '^[0-9a-f]{32}$' AND "ShareCode" IS NOT NULL))
                """);
        });
    }
}
