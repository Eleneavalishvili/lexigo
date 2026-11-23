using LanguageLearning.Core.Vocabulary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class WordReviewConfiguration : IEntityTypeConfiguration<WordReview>
{
    public void Configure(EntityTypeBuilder<WordReview> builder)
    {
        builder.HasIndex(item => new { item.SavedWordId, item.IdempotencyKey }).IsUnique();
        builder.HasOne<SavedWord>().WithMany().HasForeignKey(item => item.SavedWordId);
    }
}
