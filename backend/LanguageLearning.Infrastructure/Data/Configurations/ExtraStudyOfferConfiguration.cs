using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ExtraStudyOfferConfiguration : IEntityTypeConfiguration<ExtraStudyOffer>
{
    public void Configure(EntityTypeBuilder<ExtraStudyOffer> builder)
    {
        builder.HasKey(offer => offer.Id);
        builder.HasIndex(offer => new { offer.TeacherId, offer.CourseId }).IsUnique();
        builder.HasIndex(offer => new { offer.CourseId, offer.Enabled, offer.Id });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(offer => offer.TeacherId);
        builder.HasOne<Course>().WithMany().HasForeignKey(offer => offer.CourseId);
    }
}
