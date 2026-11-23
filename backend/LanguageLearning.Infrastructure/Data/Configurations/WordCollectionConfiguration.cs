using LanguageLearning.Core.Courses;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class WordCollectionConfiguration : IEntityTypeConfiguration<WordCollection>
{
    public void Configure(EntityTypeBuilder<WordCollection> builder)
    {
        builder.Property(item => item.Name).HasMaxLength(80);
        builder.HasIndex(item => new { item.UserId, item.CourseId, item.Name }).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId);
        builder.HasOne<Course>().WithMany().HasForeignKey(item => item.CourseId);
    }
}

public sealed class CollectionWordConfiguration : IEntityTypeConfiguration<CollectionWord>
{
    public void Configure(EntityTypeBuilder<CollectionWord> builder)
    {
        builder.HasKey(item => new { item.CollectionId, item.SavedWordId });
        builder.HasOne<WordCollection>().WithMany().HasForeignKey(item => item.CollectionId);
        builder.HasOne<SavedWord>().WithMany().HasForeignKey(item => item.SavedWordId);
    }
}
