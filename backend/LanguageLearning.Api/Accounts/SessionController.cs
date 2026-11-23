using LanguageLearning.Api.Authentication;
using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public sealed class SessionController(LoginService login, SessionService sessions, RefreshCookie cookie) : ControllerBase
{
    [HttpGet("csrf")]
    [EnableRateLimiting("session-refresh")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) => Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("login")]
    [RequireCsrf]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await login.SignInAsync(request.Email, request.Password, request.AuthenticatorCode, request.RecoveryCode,
            Request.Headers.UserAgent.ToString(), cancellationToken);
        if (result.Status == LoginStatus.RequiresTwoFactor) return Ok(new { requiresTwoFactor = true });
        if (result.Status == LoginStatus.RequiresSecuritySetup)
            return Problem(statusCode: 403, title: "Additional security setup is required for this account.");
        if (result.Ticket is null) return InvalidCredentials();
        return Complete(result.Ticket);
    }

    [HttpPost("refresh")]
    [RequireCsrf]
    [EnableRateLimiting("session-refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var ticket = await login.RefreshAsync(cookie.Read(Request), cancellationToken);
        if (ticket is not null) return Complete(ticket);
        cookie.Clear(Response);
        return InvalidCredentials();
    }

    [HttpPost("logout")]
    [RequireCsrf]
    [EnableRateLimiting("session-refresh")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessions.SignOutAsync(cookie.Read(Request), cancellationToken);
        cookie.Clear(Response);
        return NoContent();
    }

    private IActionResult Complete(AuthenticationTicket ticket)
    {
        cookie.Write(Response, ticket.RefreshToken, ticket.SessionExpiresAt);
        return Ok(new { accessToken = ticket.AccessToken.Value, expiresAt = ticket.AccessToken.ExpiresAt, user = ticket.Account });
    }

    private ObjectResult InvalidCredentials()
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return Problem(statusCode: 401, title: "Unable to sign in. Check your details and confirm your email address.");
    }
}
