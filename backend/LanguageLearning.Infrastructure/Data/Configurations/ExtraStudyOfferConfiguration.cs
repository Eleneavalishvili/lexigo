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
        builder.Property(offer => offer.PricePerLesson).HasPrecision(10, 2);
        builder.Property(offer => offer.LevelFrom).HasMaxLength(2).HasDefaultValue("A1");
        builder.Property(offer => offer.LevelTo).HasMaxLength(2).HasDefaultValue("B2");
        builder.Property(offer => offer.Biography).HasMaxLength(1000).HasDefaultValue("");
        builder.Property(offer => offer.PortraitKey).HasMaxLength(32).HasDefaultValue("default");
        builder.ToTable(table => table.HasCheckConstraint("CK_ExtraStudyOffers_Profile",
            "\"PricePerLesson\" BETWEEN 0 AND 1000 AND \"LevelFrom\" IN ('A1','A2','B1','B2','C1','C2') AND \"LevelTo\" IN ('A1','A2','B1','B2','C1','C2')"));
        builder.HasIndex(offer => new { offer.TeacherId, offer.CourseId }).IsUnique();
        builder.HasIndex(offer => new { offer.CourseId, offer.Enabled, offer.Id });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(offer => offer.TeacherId);
        builder.HasOne<Course>().WithMany().HasForeignKey(offer => offer.CourseId);
    }
}
