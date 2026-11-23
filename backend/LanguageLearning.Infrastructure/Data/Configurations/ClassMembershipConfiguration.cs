using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ClassMembershipConfiguration : IEntityTypeConfiguration<ClassMembership>
{
    public void Configure(EntityTypeBuilder<ClassMembership> builder)
    {
        builder.HasKey(item => new { item.ClassroomId, item.UserId });
        builder.Property(item => item.Status).HasMaxLength(20);
        builder.HasOne<Classroom>().WithMany().HasForeignKey(item => item.ClassroomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UserId, item.Status });
        builder.ToTable("ClassMemberships", table => table.HasCheckConstraint("CK_ClassMembership_Status", "\"Status\" IN ('Active','Left','Removed')"));
    }
}
