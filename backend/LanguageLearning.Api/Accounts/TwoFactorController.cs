using System.ComponentModel.DataAnnotations;
using LanguageLearning.Api.Authentication;
using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Route("api/v1/me/two-factor")]
[Authorize]
[EnableRateLimiting("account-entry")]
public sealed class TwoFactorController(TwoFactorService security, RefreshCookie cookie) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    [EnableRateLimiting("session-refresh")]
    public async Task<IActionResult> Status() => Ok(await security.StatusAsync(UserId));

    [HttpPost("setup")]
    public async Task<IActionResult> Setup(PasswordProof request, CancellationToken cancellationToken)
    {
        var result = await security.BeginAsync(UserId, request.CurrentPassword, cancellationToken);
        return result is null ? Invalid() : Ok(result);
    }

    [HttpPost("enable")]
    public async Task<IActionResult> Enable(AuthenticatorProof request, CancellationToken cancellationToken)
    {
        var result = await security.EnableAsync(UserId, request.CurrentPassword, request.Code, cancellationToken);
        return Codes(result);
    }

    [HttpPost("recovery-codes")]
    public async Task<IActionResult> Regenerate(AuthenticatorProof request, CancellationToken cancellationToken)
    {
        var result = await security.RegenerateAsync(UserId, request.CurrentPassword, request.Code, cancellationToken);
        return Codes(result);
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable(DisableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        if (!await security.DisableAsync(UserId, request.CurrentPassword, request.Code, request.RecoveryCode, cancellationToken)) return Invalid();
        cookie.Clear(Response);
        return Ok(new { message = "Two-factor authentication is disabled. Sign in again." });
    }

    private IActionResult Codes(string[]? codes)
    {
        if (codes is null) return Invalid();
        cookie.Clear(Response);
        return Ok(new { recoveryCodes = codes });
    }

    private ObjectResult Invalid() => Problem(statusCode: 400, title: "Unable to change security settings. Check your details and use a fresh authenticator code. Privileged accounts must keep two-factor authentication enabled.");
}

public sealed record PasswordProof([Required, StringLength(128)] string CurrentPassword);
public sealed record AuthenticatorProof([Required, StringLength(128)] string CurrentPassword, [Required, RegularExpression("^[0-9]{6}$")] string Code);
public sealed record DisableTwoFactorRequest([Required, StringLength(128)] string CurrentPassword,
    [StringLength(12)] string? Code, [StringLength(32)] string? RecoveryCode);
