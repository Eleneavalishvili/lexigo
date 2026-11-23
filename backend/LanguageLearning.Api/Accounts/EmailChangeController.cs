using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[EnableRateLimiting("account-entry")]
public sealed class EmailChangeController(EmailChangeService changes) : ControllerBase
{
    [Authorize]
    [HttpPut("api/v1/me/email")]
    public async Task<IActionResult> RequestChange(EmailChangeRequest request, CancellationToken cancellationToken)
    {
        Guid userId = Guid.Parse(User.FindFirst("sub")!.Value);
        if (!await changes.RequestAsync(userId, request.NewEmail, request.CurrentPassword,
            request.AuthenticatorCode, request.RecoveryCode, cancellationToken))
            return Problem(statusCode: 400, title: "Check your current password and security code, then try again.");
        return Accepted(new { message = "If this address is available, a confirmation link will be sent. Your current email remains unchanged until confirmation." });
    }

    [AllowAnonymous]
    [HttpPost("api/v1/auth/confirm-email-change")]
    public async Task<IActionResult> Confirm(ConfirmEmailChangeRequest request, CancellationToken cancellationToken) =>
        await changes.ConfirmAsync(request.UserId, request.RequestId, request.Token, cancellationToken)
            ? Ok(new { message = "Email changed. Sign in with your new address." })
            : Problem(statusCode: 400, title: "This email change link is invalid, unavailable, already used or expired.");
}

public sealed record EmailChangeRequest(
    [Required, EmailAddress, StringLength(256)] string NewEmail,
    [Required, StringLength(128)] string CurrentPassword,
    [StringLength(32)] string? AuthenticatorCode = null,
    [StringLength(32)] string? RecoveryCode = null);

public sealed record ConfirmEmailChangeRequest(Guid UserId, Guid RequestId, [Required, StringLength(4096)] string Token);
