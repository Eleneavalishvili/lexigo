using LanguageLearning.Core.Courses;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class VocabularyRevisionConfiguration : IEntityTypeConfiguration<VocabularyRevision>
{
    public void Configure(EntityTypeBuilder<VocabularyRevision> builder)
    {
        builder.Property(item => item.Word).HasMaxLength(160);
        builder.Property(item => item.EnglishTranslation).HasMaxLength(300);
        builder.Property(item => item.Example).HasMaxLength(2000);
        builder.Property(item => item.PartOfSpeech).HasMaxLength(60);
        builder.Property(item => item.Topic).HasMaxLength(60);
        builder.Property(item => item.Status).HasMaxLength(16);
        builder.Property(item => item.ReviewComment).HasMaxLength(1000);
        builder.HasOne<Course>().WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VocabularyItem>().WithMany().HasForeignKey(item => item.WordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.PublisherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.CourseId, item.CreatedAt });
        builder.HasIndex(item => item.WordId).IsUnique().HasFilter("\"Status\" NOT IN ('Published', 'Withdrawn')");
        builder.ToTable(table => table.HasCheckConstraint("CK_VocabularyRevision_Workflow", """
            "Status" IN ('Draft', 'Submitted', 'Approved', 'Published', 'Withdrawn')
            AND ("ReviewerId" IS NULL OR "ReviewerId" <> "AuthorId")
            AND ("Status" NOT IN ('Approved', 'Published') OR ("ReviewerId" IS NOT NULL AND "ReviewedAt" IS NOT NULL))
            AND ("Status" <> 'Published' OR ("PublisherId" IS NOT NULL AND "PublishedAt" IS NOT NULL AND "WordId" IS NOT NULL))
            """));
    }
}
