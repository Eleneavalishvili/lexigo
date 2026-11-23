using LanguageLearning.Core.Classrooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class ClassInvitationConfiguration : IEntityTypeConfiguration<ClassInvitation>
{
    public void Configure(EntityTypeBuilder<ClassInvitation> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CodeHash).HasMaxLength(64);
        builder.HasIndex(item => item.CodeHash).IsUnique();
        builder.HasOne<Classroom>().WithMany().HasForeignKey(item => item.ClassroomId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("ClassInvitations", table => table.HasCheckConstraint("CK_ClassInvitation_Uses",
            "\"MaxUses\" BETWEEN 1 AND 200 AND \"UsedCount\" BETWEEN 0 AND \"MaxUses\""));
    }
}
