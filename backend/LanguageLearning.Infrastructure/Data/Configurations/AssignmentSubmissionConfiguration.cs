using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class AssignmentSubmissionConfiguration : IEntityTypeConfiguration<AssignmentSubmission>
{
    public void Configure(EntityTypeBuilder<AssignmentSubmission> builder)
    {
        builder.HasIndex(item => new { item.AssignmentId, item.UserId, item.SessionId }).IsUnique();
        builder.HasOne<Assignment>().WithMany().HasForeignKey(item => item.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LearningSession>().WithMany().HasForeignKey(item => new { item.UserId, item.SessionId })
            .HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
