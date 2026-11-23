using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(160);
        builder.Property(item => item.Description).HasMaxLength(3000);
        builder.Property(item => item.Difficulty).HasMaxLength(40);
        builder.HasIndex(item => new { item.CourseId, item.OrderNumber }).IsUnique();
        builder.HasOne<Course>().WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("Units", table => table.HasCheckConstraint("CK_Units_Order", "\"OrderNumber\" > 0"));
    }
}

