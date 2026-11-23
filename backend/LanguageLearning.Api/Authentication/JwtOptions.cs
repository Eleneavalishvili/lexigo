namespace LanguageLearning.Api.Authentication;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "LexiGo";
    public string Audience { get; set; } = "LexiGo.Web";
    public int AccessMinutes { get; set; } = 5;
    public string? SigningPrivateKeyPem { get; set; }
}
