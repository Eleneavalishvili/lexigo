using LanguageLearning.Core.Content;
using LanguageLearning.Core.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class SessionOptionAudioConfiguration : IEntityTypeConfiguration<SessionOptionAudio>
{
    public void Configure(EntityTypeBuilder<SessionOptionAudio> builder)
    {
        builder.HasKey(item => new { item.SessionExerciseId, item.OptionId });
        builder.HasOne<SessionExercise>().WithMany().HasForeignKey(item => item.SessionExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(item => item.AudioMediaId).OnDelete(DeleteBehavior.Restrict);
    }
}
