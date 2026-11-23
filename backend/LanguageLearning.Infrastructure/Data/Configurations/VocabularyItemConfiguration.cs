using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Core.Vocabulary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class VocabularyItemConfiguration : IEntityTypeConfiguration<VocabularyItem>
{
    public void Configure(EntityTypeBuilder<VocabularyItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Word).HasMaxLength(160).IsRequired();
        builder.Property(item => item.EnglishTranslation).HasMaxLength(300).IsRequired();
        builder.Property(item => item.Example).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.PartOfSpeech).HasMaxLength(60).IsRequired();
        builder.Property(item => item.Topic).HasMaxLength(60);
        builder.HasIndex(item => new { item.CourseId, item.Word });
        builder.HasOne<Course>().WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(item => item.AudioMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_VocabularyItem_Text", "length(btrim(\"Word\")) > 0 AND length(btrim(\"EnglishTranslation\")) > 0"));
    }
}
