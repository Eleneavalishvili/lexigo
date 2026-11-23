using LanguageLearning.Core.Accounts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LanguageLearning.Api.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddAccountAuthentication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Authentication"))
            .Validate(options => options.AccessMinutes is >= 1 and <= 10, "Use a short access-token lifetime.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer) && !string.IsNullOrWhiteSpace(options.Audience), "Configure the token issuer and audience.")
            .Validate(options => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(options.SigningPrivateKeyPem), "Configure a production signing key.")
            .ValidateOnStart();
        services.AddSingleton<SigningKeyProvider>();
        services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddScoped<SessionValidationEvents>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<SigningKeyProvider, IOptions<JwtOptions>>((bearer, keys, settings) =>
        {
            bearer.MapInboundClaims = false;
            bearer.IncludeErrorDetails = false;
            bearer.EventsType = typeof(SessionValidationEvents);
            bearer.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidIssuer = settings.Value.Issuer,
                ValidAudience = settings.Value.Audience,
                IssuerSigningKey = keys.Key,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
        });
        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = environment.IsDevelopment() ? "lexigo.csrf" : "__Host-lexigo.csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        return services;
    }
}
