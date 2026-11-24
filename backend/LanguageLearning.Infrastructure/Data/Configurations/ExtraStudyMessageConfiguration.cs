using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ExtraStudyMessageConfiguration : IEntityTypeConfiguration<ExtraStudyMessage>
{
    public void Configure(EntityTypeBuilder<ExtraStudyMessage> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Body).HasMaxLength(2000);
        builder.HasIndex(item => new { item.RequestId, item.CreatedAt, item.Id });
        builder.HasOne<ExtraStudyRequest>().WithMany().HasForeignKey(item => item.RequestId);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
