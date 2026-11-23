using LanguageLearning.Api.Authentication;
using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting("account-entry")]
public sealed class PasswordRecoveryController(PasswordRecoveryService recovery, RefreshCookie cookie) : ControllerBase
{
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken cancellationToken)
    {
        await recovery.RequestResetAsync(request.Email, cancellationToken);
        return Accepted(new { message = "If this account can be recovered, a reset link will arrive shortly. Your password has not changed." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!await recovery.ResetAsync(request.UserId, request.Token, request.NewPassword, cancellationToken))
            return Problem(statusCode: 400, title: "This reset request is invalid or has expired.");
        cookie.Clear(Response);
        return Ok(new { message = "Your password is updated. Sign in again with your new password." });
    }
}
