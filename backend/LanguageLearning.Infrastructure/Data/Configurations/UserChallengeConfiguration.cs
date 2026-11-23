using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class UserChallengeConfiguration : IEntityTypeConfiguration<UserChallenge>
{
    public void Configure(EntityTypeBuilder<UserChallenge> builder)
    {
        builder.HasKey(item => new { item.UserId, item.ChallengeId });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Challenge>().WithMany().HasForeignKey(item => item.ChallengeId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("UserChallenges", table =>
        {
            table.HasCheckConstraint("CK_UserChallenge_Progress", "\"Progress\" >= 0");
            table.HasCheckConstraint("CK_UserChallenge_Reward", "\"RewardedAt\" IS NULL OR (\"CompletedAt\" IS NOT NULL AND \"RewardedAt\" >= \"CompletedAt\")");
        });
    }
}
