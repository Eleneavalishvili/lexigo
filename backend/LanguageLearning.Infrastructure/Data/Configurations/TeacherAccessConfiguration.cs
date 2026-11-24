using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class TeacherAccessConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.TeacherAccessCodeHash).HasMaxLength(64);
        builder.HasIndex(user => user.TeacherAccessCodeHash).IsUnique();
    }
}
