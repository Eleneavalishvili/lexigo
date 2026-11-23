using LanguageLearning.Core.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.HasKey(language => language.Id);
        builder.Property(language => language.Code).HasMaxLength(10);
        builder.HasIndex(language => language.Code).IsUnique();
        builder.Property(language => language.EnglishName).HasMaxLength(80);
        builder.Property(language => language.NativeName).HasMaxLength(80);
        builder.Property(language => language.WritingDirection).HasMaxLength(3);
        builder.HasData(
            new Language { Id = 1, Code = "en", EnglishName = "English", NativeName = "English" },
            new Language { Id = 2, Code = "es", EnglishName = "Spanish", NativeName = "Español" },
            new Language { Id = 3, Code = "it", EnglishName = "Italian", NativeName = "Italiano" },
            new Language { Id = 4, Code = "de", EnglishName = "German", NativeName = "Deutsch" },
            new Language { Id = 5, Code = "ru", EnglishName = "Russian", NativeName = "Русский" });
    }
}
