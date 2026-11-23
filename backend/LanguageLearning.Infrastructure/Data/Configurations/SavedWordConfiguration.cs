using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class SavedWordConfiguration : IEntityTypeConfiguration<SavedWord>
{
    public void Configure(EntityTypeBuilder<SavedWord> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.UserId, item.VocabularyItemId }).IsUnique();
        builder.HasIndex(item => new { item.UserId, item.DueAt, item.Id });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VocabularyItem>().WithMany().HasForeignKey(item => item.VocabularyItemId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_SavedWord_Schedule", "\"CorrectStreak\" >= 0 AND \"IntervalDays\" IN (0,1,3,7,14,30)"));
    }
}
