using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class TeacherFeedbackConfiguration : IEntityTypeConfiguration<TeacherFeedback>
{
    public void Configure(EntityTypeBuilder<TeacherFeedback> builder)
    {
        builder.Property(item => item.Message).HasMaxLength(1000);
        builder.HasIndex(item => item.SubmissionId).IsUnique();
        builder.HasOne<AssignmentSubmission>().WithMany().HasForeignKey(item => item.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
