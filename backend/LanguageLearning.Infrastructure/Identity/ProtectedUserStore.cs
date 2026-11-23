using LanguageLearning.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Identity;

public sealed class ProtectedUserStore(LexiGoDbContext database, IDataProtectionProvider protection)
    : UserStore<ApplicationUser, IdentityRole<Guid>, LexiGoDbContext, Guid>(database)
{
    private static bool IsSecret(string provider, string name) =>
        (provider == "[AspNetUserStore]" && name is "AuthenticatorKey" or "RecoveryCodes")
        || (provider == "LexiGo" && name is "AuthenticatorReplayHistory" or "PendingEmailChange" or "PendingAuthenticatorReplacement");

    public override Task SetTokenAsync(ApplicationUser user, string loginProvider, string name, string? value, CancellationToken cancellationToken)
    {
        if (value is not null && IsSecret(loginProvider, name))
            value = Protector(user.Id, name).Protect(value);
        return base.SetTokenAsync(user, loginProvider, name, value, cancellationToken);
    }

    public override async Task<string?> GetTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        string? value = await base.GetTokenAsync(user, loginProvider, name, cancellationToken);
        return value is not null && IsSecret(loginProvider, name) ? Protector(user.Id, name).Unprotect(value) : value;
    }

    private IDataProtector Protector(Guid userId, string name) =>
        protection.CreateProtector("LexiGo.IdentitySecrets.v1", userId.ToString("N"), name);
}
