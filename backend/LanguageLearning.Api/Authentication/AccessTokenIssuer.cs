using LanguageLearning.Core.Accounts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LanguageLearning.Api.Authentication;

public sealed class AccessTokenIssuer(SigningKeyProvider keys, IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(Guid userId, Guid familyId, IReadOnlyList<string> roles, bool mfaVerified)
    {
        DateTimeOffset now = clock.GetUtcNow();
        DateTimeOffset expires = now.AddMinutes(options.Value.AccessMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Value.Issuer,
            Audience = options.Value.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(keys.Key, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(),
                ["sid"] = familyId.ToString(),
                ["jti"] = Guid.NewGuid().ToString(),
                ["role"] = roles.ToArray(),
                ["amr"] = mfaVerified ? new[] { "pwd", "mfa" } : new[] { "pwd" }
            }
        };
        return new(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
