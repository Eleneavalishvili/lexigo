using LanguageLearning.Core.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(course => course.Id);
        builder.Property(course => course.Title).HasMaxLength(120);
        builder.Property(course => course.Description).HasMaxLength(1000);
        builder.HasIndex(course => new { course.SourceLanguageId, course.TargetLanguageId }).IsUnique();
        builder.HasOne(course => course.SourceLanguage).WithMany().HasForeignKey(course => course.SourceLanguageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(course => course.TargetLanguage).WithMany().HasForeignKey(course => course.TargetLanguageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(
            Seed("10000000-0000-0000-0000-000000000001", 2, "Spanish", 1),
            Seed("10000000-0000-0000-0000-000000000002", 3, "Italian", 2),
            Seed("10000000-0000-0000-0000-000000000003", 4, "German", 3),
            Seed("10000000-0000-0000-0000-000000000004", 5, "Russian", 4));
    }

    private static Course Seed(string id, int target, string name, int order) => new()
    {
        Id = Guid.Parse(id),
        SourceLanguageId = 1,
        TargetLanguageId = target,
        Title = name,
        Description = $"Learn {name} through English.",
        OrderNumber = order,
        IsPublished = false
    };
}
