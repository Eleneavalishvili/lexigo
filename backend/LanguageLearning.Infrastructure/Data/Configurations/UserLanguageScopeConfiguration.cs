using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class UserLanguageScopeConfiguration : IEntityTypeConfiguration<UserLanguageScope>
{
    public void Configure(EntityTypeBuilder<UserLanguageScope> builder)
    {
        builder.HasKey(item => new { item.UserId, item.LanguageId });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Language>().WithMany().HasForeignKey(item => item.LanguageId).OnDelete(DeleteBehavior.Restrict);
    }
}
