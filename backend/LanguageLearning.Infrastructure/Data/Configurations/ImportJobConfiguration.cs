using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.Property(item => item.Status).HasMaxLength(20);
        builder.Property(item => item.FileKey).HasMaxLength(100);
        builder.Property(item => item.ValidationErrors).HasColumnType("jsonb");
        builder.Property(item => item.Payload).HasColumnType("jsonb");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Lesson>().WithMany().HasForeignKey(item => item.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("ImportJobs", table => table.HasCheckConstraint("CK_ImportJobs_State",
            "(\"Status\" IN ('Valid', 'Invalid') AND \"LessonId\" IS NULL) OR (\"Status\" = 'Committed' AND \"LessonId\" IS NOT NULL)"));
    }
}
