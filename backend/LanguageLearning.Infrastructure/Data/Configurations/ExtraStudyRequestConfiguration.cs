using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ExtraStudyRequestConfiguration : IEntityTypeConfiguration<ExtraStudyRequest>
{
    public void Configure(EntityTypeBuilder<ExtraStudyRequest> builder)
    {
        builder.HasIndex(item => new { item.OfferId, item.LearnerId }).IsUnique();
        builder.HasIndex(item => new { item.LearnerId, item.Id });
        builder.Property(item => item.Status).HasMaxLength(12);
        builder.HasOne<ExtraStudyOffer>().WithMany().HasForeignKey(item => item.OfferId);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.LearnerId);
        builder.ToTable("ExtraStudyRequests", table => table.HasCheckConstraint(
            "CK_ExtraStudyRequests_Status", """ "Status" IN ('Pending', 'Accepted', 'Declined', 'Closed') """));
    }
}
