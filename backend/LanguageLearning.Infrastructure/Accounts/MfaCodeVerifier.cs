using System.Globalization;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class MfaCodeVerifier(UserManager<ApplicationUser> users, TimeProvider clock)
{
    // The surrounding login/settings transaction holds the user row lock.
    public async Task<bool> VerifyAsync(ApplicationUser user, string code)
    {
        code = code.Replace(" ", "").Replace("-", "");
        if (!await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code)) return false;
        string hash = SessionService.Hash(code);
        string? previous = await users.GetAuthenticationTokenAsync(user, "LexiGo", "AuthenticatorReplayHistory");
        long now = clock.GetUtcNow().ToUnixTimeSeconds();
        var recent = new List<string>();
        foreach (string entry in previous?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? [])
        {
            var parts = entry.Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[1], out long used)) return false;
            if (now - used >= 180) continue;
            if (parts[0] == hash) return false;
            recent.Add(entry);
        }
        if (recent.Count >= 32) return false;
        recent.Add(hash + ":" + now.ToString(CultureInfo.InvariantCulture));
        var result = await users.SetAuthenticationTokenAsync(user, "LexiGo", "AuthenticatorReplayHistory",
            string.Join(';', recent));
        return result.Succeeded;
    }
}
