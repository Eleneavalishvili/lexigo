using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.HasKey(item => item.Key);
        builder.Property(item => item.Key).HasMaxLength(80);
        builder.Property(item => item.Value).HasColumnType("jsonb");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
