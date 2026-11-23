using LanguageLearning.Core.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
{
    public void Configure(EntityTypeBuilder<Challenge> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(120);
        builder.Property(item => item.RuleType).HasMaxLength(40);
        builder.HasIndex(item => new { item.StartsAt, item.EndsAt });
        builder.Property(item => item.Revision).HasDefaultValue(1);
        builder.HasIndex(item => new { item.RuleType, item.StartsAt, item.EndsAt }).IsUnique()
            .HasFilter("\"IsArchived\" = false");
        builder.ToTable("Challenges", table =>
        {
            table.HasCheckConstraint("CK_Challenge_Rule", "\"RuleType\" IN ('CompletedLessons','StudyDays')");
            table.HasCheckConstraint("CK_Challenge_Values", "\"Target\" BETWEEN 1 AND 10000 AND \"RewardXp\" BETWEEN 1 AND 1000");
            table.HasCheckConstraint("CK_Challenge_Window", "\"EndsAt\" > \"StartsAt\"");
        });
    }
}
