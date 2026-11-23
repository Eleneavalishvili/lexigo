namespace LanguageLearning.Core.Accounts;

public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAt);

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(Guid userId, Guid familyId, IReadOnlyList<string> roles, bool mfaVerified);
}
