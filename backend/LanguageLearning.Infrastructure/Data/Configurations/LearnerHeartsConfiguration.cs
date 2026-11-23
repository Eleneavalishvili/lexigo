using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class LearnerHeartsConfiguration : IEntityTypeConfiguration<LearnerHearts>
{
    public void Configure(EntityTypeBuilder<LearnerHearts> builder)
    {
        builder.HasKey(item => item.UserId);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_LearnerHearts_Balance",
            "\"Count\" BETWEEN 0 AND 5 AND ((\"Count\" = 0 AND \"RefillAt\" IS NOT NULL) OR (\"Count\" > 0 AND \"RefillAt\" IS NULL))"));
    }
}
