using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.Property(session => session.SecurityStamp).HasMaxLength(256).IsRequired();
        builder.Property(session => session.DeviceLabel).HasMaxLength(160).IsRequired();
        builder.Property(session => session.RevocationReason).HasMaxLength(40);
        builder.HasIndex(session => session.FamilyId).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        builder.HasIndex(session => new { session.UserId, session.ExpiresAt });
        builder.HasOne(session => session.User).WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RefreshSession>().WithMany().HasForeignKey(session => session.ReplacedById).OnDelete(DeleteBehavior.Restrict);
    }
}
