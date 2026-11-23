using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Purpose).HasMaxLength(40).HasDefaultValue("EmailConfirmation");
        builder.HasIndex(message => new { message.UserId, message.Purpose, message.CreatedAt });
        builder.Property(message => message.Recipient).HasMaxLength(256);
        builder.Property(message => message.Subject).HasMaxLength(200);
        builder.HasIndex(message => new { message.SentAt, message.NextAttemptAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(message => message.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
