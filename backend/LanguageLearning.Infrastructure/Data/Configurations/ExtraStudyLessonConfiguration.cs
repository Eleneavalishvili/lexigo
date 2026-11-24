using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ExtraStudyLessonConfiguration : IEntityTypeConfiguration<ExtraStudyLesson>
{
    public void Configure(EntityTypeBuilder<ExtraStudyLesson> builder)
    {
        builder.HasKey(item => new { item.RequestId, item.LessonId });
        builder.ToTable(table => table.HasCheckConstraint("CK_ExtraStudyLessons_Schedule",
            "(\"ScheduledAt\" IS NULL AND \"DurationMinutes\" IS NULL) OR (\"ScheduledAt\" IS NOT NULL AND \"DurationMinutes\" IS NOT NULL AND \"DurationMinutes\" BETWEEN 15 AND 120)"));
        builder.HasIndex(item => item.ScheduledAt);
        builder.HasOne<ExtraStudyRequest>().WithMany().HasForeignKey(item => item.RequestId);
        builder.HasOne<Lesson>().WithMany().HasForeignKey(item => item.LessonId);
    }
}
