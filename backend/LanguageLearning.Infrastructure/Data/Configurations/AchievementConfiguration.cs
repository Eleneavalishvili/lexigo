using LanguageLearning.Core.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(60);
        builder.Property(item => item.Title).HasMaxLength(120);
        builder.Property(item => item.RuleType).HasMaxLength(40);
        builder.HasIndex(item => item.Code).IsUnique();
        builder.Property(item => item.Revision).HasDefaultValue(1);
        builder.ToTable("Achievements", table => table.HasCheckConstraint("CK_Achievement_Rule",
            "\"Threshold\" > 0 AND \"RuleType\" IN ('CompletedLessons','GoalStreak','SavedWords','PerfectLesson')"));
        builder.HasData(
            new Achievement { Id = Guid.Parse("40000000-0000-0000-0000-000000000001"), Code = "first-lesson", Title = "Your first little step", RuleType = "CompletedLessons", Threshold = 1 },
            new Achievement { Id = Guid.Parse("40000000-0000-0000-0000-000000000002"), Code = "goal-streak", Title = "A rhythm of your own", RuleType = "GoalStreak", Threshold = 3 },
            new Achievement { Id = Guid.Parse("40000000-0000-0000-0000-000000000003"), Code = "word-collector", Title = "A pocketful of words", RuleType = "SavedWords", Threshold = 10 },
            new Achievement { Id = Guid.Parse("40000000-0000-0000-0000-000000000004"), Code = "perfect-lesson", Title = "A clear little victory", RuleType = "PerfectLesson", Threshold = 1 });
    }
}
