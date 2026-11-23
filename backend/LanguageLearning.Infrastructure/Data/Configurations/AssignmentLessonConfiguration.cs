using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class AssignmentLessonConfiguration : IEntityTypeConfiguration<AssignmentLesson>
{
    public void Configure(EntityTypeBuilder<AssignmentLesson> builder)
    {
        builder.HasKey(item => new { item.AssignmentId, item.LessonId });
        builder.HasOne<Assignment>().WithMany().HasForeignKey(item => item.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Lesson>().WithMany().HasForeignKey(item => item.LessonId).OnDelete(DeleteBehavior.Restrict);
    }
}
