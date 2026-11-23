using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.StorageKey).HasMaxLength(300);
        builder.HasIndex(item => item.StorageKey).IsUnique();
        builder.Property(item => item.MimeType).HasMaxLength(100);
        builder.Property(item => item.Source).HasMaxLength(2000);
        builder.Property(item => item.License).HasMaxLength(1000);
        builder.Property(item => item.StorageState).HasMaxLength(16).HasDefaultValue("Ready");
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(item => item.Sha256).HasMaxLength(64);
        builder.HasIndex(item => new { item.StorageState, item.CreatedAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.VerifiedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Language>().WithMany().HasForeignKey(item => item.LanguageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<ApplicationUser>().WithOne()
            .HasForeignKey(user => new { user.Id, user.AvatarMediaId })
            .HasPrincipalKey(media => new { media.UploadedBy, media.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("MediaAssets", table =>
        {
            table.HasCheckConstraint("CK_MediaAssets_Size", "\"ByteSize\" > 0");
            table.HasCheckConstraint("CK_MediaAssets_State", "\"StorageState\" IN ('Pending', 'Ready', 'Deleting') AND (NOT \"IsVerified\" OR \"StorageState\" = 'Ready')");
            table.HasCheckConstraint("CK_MediaAssets_Duration", "\"DurationMilliseconds\" IS NULL OR \"DurationMilliseconds\" BETWEEN 100 AND 120000");
            table.HasCheckConstraint("CK_MediaAssets_Review", "(\"VerifiedBy\" IS NULL) = (\"VerifiedAt\" IS NULL) AND (\"VerifiedBy\" IS NULL OR (\"VerifiedBy\" <> \"UploadedBy\" AND \"IsVerified\"))");
        });
    }
}

