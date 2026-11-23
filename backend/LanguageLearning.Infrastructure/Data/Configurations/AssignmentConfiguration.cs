using LanguageLearning.Core.Classrooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.Property(item => item.Title).HasMaxLength(120);
        builder.Property(item => item.RequestFingerprint).HasMaxLength(64);
        builder.HasIndex(item => new { item.ClassroomId, item.RequestKey }).IsUnique();
        builder.HasIndex(item => new { item.ClassroomId, item.DueAt, item.Id });
        builder.HasOne<Classroom>().WithMany().HasForeignKey(item => item.ClassroomId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("Assignments", table =>
        {
            table.HasCheckConstraint("CK_Assignment_MinimumScore", "\"MinimumScore\" BETWEEN 0 AND 100");
            table.HasCheckConstraint("CK_Assignment_Deadline", "\"DueAt\" > \"CreatedAt\"");
            table.HasCheckConstraint("CK_Assignment_Revision", "\"Revision\" > 0");
        });
    }
}
