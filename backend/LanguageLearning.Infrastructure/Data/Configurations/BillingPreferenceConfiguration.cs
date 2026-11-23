using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class BillingPreferenceConfiguration : IEntityTypeConfiguration<BillingPreference>
{
    public void Configure(EntityTypeBuilder<BillingPreference> builder)
    {
        builder.HasKey(item => item.UserId);
        builder.HasOne<Subscription>().WithMany().HasForeignKey(item => new { item.UserId, item.RenewalConsentSubscriptionId })
            .HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_BillingPreference_Consent",
            "(\"RenewalConsentSubscriptionId\" IS NULL AND \"RenewalConsentAt\" IS NULL) OR (\"AutoRenew\" AND \"RenewalConsentSubscriptionId\" IS NOT NULL AND \"RenewalConsentAt\" IS NOT NULL)"));
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TestCard>().WithMany().HasForeignKey(item => new { item.UserId, item.DefaultTestCardId })
            .HasPrincipalKey(item => new { item.UserId, item.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
