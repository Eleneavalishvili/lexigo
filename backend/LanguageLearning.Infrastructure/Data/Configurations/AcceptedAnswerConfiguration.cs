using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class AcceptedAnswerConfiguration : IEntityTypeConfiguration<AcceptedAnswer>
{
    public void Configure(EntityTypeBuilder<AcceptedAnswer> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AnswerText).HasMaxLength(5000);
        builder.Property(item => item.OrderedTokenIds).HasColumnType("jsonb");
        builder.HasOne<Exercise>().WithMany().HasForeignKey(item => item.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}

