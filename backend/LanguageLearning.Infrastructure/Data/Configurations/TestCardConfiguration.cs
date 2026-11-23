using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class TestCardConfiguration : IEntityTypeConfiguration<TestCard>
{
    public void Configure(EntityTypeBuilder<TestCard> builder)
    {
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.UserId, item.Id });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Nickname).HasMaxLength(40);
        builder.Property(item => item.DisplayLastFour).HasMaxLength(4);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.Property(item => item.Status).HasMaxLength(12);
        builder.Property(item => item.Balance).HasPrecision(18, 2);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.ToTable(table => table.HasCheckConstraint("CK_TestCard_Values",
            "\"Balance\" >= 0 AND \"Version\" > 0 AND \"Status\" IN ('Active','Blocked') AND \"Currency\" IN ('GEL','USD','EUR') AND \"DisplayLastFour\" ~ '^[0-9]{4}$'"));
    }
}
