using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class XpTransactionConfiguration : IEntityTypeConfiguration<XpTransaction>
{
    public void Configure(EntityTypeBuilder<XpTransaction> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SourceType).HasMaxLength(40);
        builder.HasIndex(item => new { item.UserId, item.SourceType, item.SourceId }).IsUnique();
        builder.HasIndex(item => new { item.CourseId, item.EarnedAt, item.UserId });
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(item => new { item.UserId, item.CourseId }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("XpTransactions", table => table.HasCheckConstraint("CK_XpTransactions_Amount", "\"Amount\" > 0"));
    }
}
