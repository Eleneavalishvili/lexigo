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
        builder.HasOne<ExtraStudyRequest>().WithMany().HasForeignKey(item => item.RequestId);
        builder.HasOne<Lesson>().WithMany().HasForeignKey(item => item.LessonId);
    }
}
