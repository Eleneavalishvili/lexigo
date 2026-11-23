using LanguageLearning.Core.Accounts;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed record AuthenticationTicket(IssuedAccessToken AccessToken, string RefreshToken, DateTimeOffset SessionExpiresAt, AccountSummary Account);
public enum LoginStatus { InvalidCredentials, RequiresTwoFactor, RequiresSecuritySetup, Success }
public sealed record LoginResult(LoginStatus Status, AuthenticationTicket? Ticket = null);
