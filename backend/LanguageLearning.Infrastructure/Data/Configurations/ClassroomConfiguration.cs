using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ClassroomConfiguration : IEntityTypeConfiguration<Classroom>
{
    public void Configure(EntityTypeBuilder<Classroom> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(120);
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.Revision).HasDefaultValue(1);
        builder.Property(item => item.CreationFingerprint).HasMaxLength(64);
        builder.HasIndex(item => new { item.TeacherId, item.CreationKey }).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.TeacherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>().WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TeacherId, item.IsArchived });
        builder.ToTable("Classrooms", table => table.HasCheckConstraint("CK_Classroom_Revision", "\"Revision\" > 0"));
    }
}
