using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ContentReviewConfiguration : IEntityTypeConfiguration<ContentReview>
{
    public void Configure(EntityTypeBuilder<ContentReview> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Decision).HasMaxLength(20);
        builder.Property(item => item.Comment).HasMaxLength(3000);
        builder.HasOne<LessonVersion>().WithMany().HasForeignKey(item => item.LessonVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.LessonVersionId, item.ReviewedAt });
        builder.ToTable("ContentReviews", table => table.HasCheckConstraint("CK_ContentReviews_Decision", "\"Decision\" IN ('Approved','ChangesRequested')"));
    }
}

