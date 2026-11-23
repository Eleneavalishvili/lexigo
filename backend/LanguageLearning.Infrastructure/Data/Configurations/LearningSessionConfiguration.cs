using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class LearningSessionConfiguration : IEntityTypeConfiguration<LearningSession>
{
    public void Configure(EntityTypeBuilder<LearningSession> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.UserId, item.Id });
        builder.Property(item => item.Mode).HasMaxLength(20);
        builder.Property(item => item.PracticeFocus).HasMaxLength(20);
        builder.HasIndex(item => new { item.UserId, item.CourseId, item.PracticeFocus }).IsUnique()
            .HasFilter("\"PracticeFocus\" IS NOT NULL AND \"CompletedAt\" IS NULL");
        builder.Property(item => item.Title).HasMaxLength(160);
        builder.Property(item => item.Explanation).HasMaxLength(20000);
        builder.Property(item => item.PlacementMap).HasColumnType("jsonb");
        builder.HasOne<Skill>().WithMany().HasForeignKey(item => new { item.CourseId, item.PracticeSkillId })
            .HasPrincipalKey(item => new { item.CourseId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UserId, item.CourseId, item.PracticeSkillId }).IsUnique()
            .HasFilter("\"PracticeSkillId\" IS NOT NULL AND \"CompletedAt\" IS NULL");
        builder.ToTable(table => table.HasCheckConstraint("CK_LearningSessions_PracticeSkill",
            """ "PracticeSkillId" IS NULL OR ("Mode" = 'Practice' AND "LessonId" IS NULL AND "PlacementMap" IS NULL AND "FirstCompletionXp" = 0 AND "AwardedXp" = 0) """));
        builder.HasOne<Unit>().WithMany().HasForeignKey(item => new { item.CourseId, item.RecommendedUnitId })
            .HasPrincipalKey(item => new { item.CourseId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UserId, item.LessonId, item.Mode }).IsUnique().HasFilter("\"CompletedAt\" IS NULL");
        builder.HasIndex(item => new { item.UserId, item.CourseId }).IsUnique()
            .HasFilter("\"PlacementMap\" IS NOT NULL AND \"CompletedAt\" IS NULL");
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(item => new { item.UserId, item.CourseId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LessonVersion>().WithMany().HasForeignKey(item => new { item.LessonId, item.LessonVersionId }).HasPrincipalKey(item => new { item.LessonId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("LearningSessions", table =>
        {
            table.HasCheckConstraint("CK_LearningSessions_PracticeFocus", """
                "PracticeFocus" IS NULL OR ("PracticeFocus" IN ('repeated', 'due') AND "Mode" = 'Practice'
                    AND "LessonId" IS NULL AND "PlacementMap" IS NULL AND "PracticeSkillId" IS NULL
                    AND "FirstCompletionXp" = 0 AND "AwardedXp" = 0)
                """);
            table.HasCheckConstraint("CK_LearningSessions_Values", "\"Hearts\" BETWEEN 0 AND 5 AND \"Score\" BETWEEN 0 AND 100 AND \"AwardedXp\" >= 0 AND \"FirstCompletionXp\" BETWEEN 0 AND 1000 AND \"PassScore\" BETWEEN 1 AND 100");
            table.HasCheckConstraint("CK_LearningSessions_Mode", "\"Mode\" IN ('Lesson','Practice')");
            table.HasCheckConstraint("CK_LearningSessions_Placement", "(\"PlacementMap\" IS NULL AND \"RecommendedUnitId\" IS NULL AND \"PlacementAppliedAt\" IS NULL) OR (\"PlacementMap\" IS NOT NULL AND \"Mode\" = 'Practice' AND \"LessonId\" IS NULL AND \"FirstCompletionXp\" = 0 AND \"AwardedXp\" = 0 AND ((\"PlacementAppliedAt\" IS NULL AND \"RecommendedUnitId\" IS NULL) OR (\"PlacementAppliedAt\" IS NOT NULL AND \"RecommendedUnitId\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL)))");
        });
    }
}
