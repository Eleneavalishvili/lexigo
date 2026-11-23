using System.ComponentModel.DataAnnotations;
using LanguageLearning.Api.Authentication;
using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Authorize]
[Route("api/v1/me/two-factor/replacement")]
[EnableRateLimiting("account-entry")]
public sealed class AuthenticatorReplacementController(AuthenticatorReplacementService replacement, RefreshCookie cookie) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpPost]
    public async Task<IActionResult> Begin(PasswordProof request, CancellationToken cancellationToken)
    {
        var result = await replacement.BeginAsync(UserId, FamilyId, request.CurrentPassword, cancellationToken);
        return result is null ? Invalid() : Ok(result);
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(ReplacementProof request, CancellationToken cancellationToken)
    {
        var result = await replacement.ConfirmAsync(UserId, FamilyId, request.RequestId, request.CurrentPassword, request.Code, cancellationToken);
        if (result is null) return Invalid();
        cookie.Clear(Response);
        return Ok(new { recoveryCodes = result });
    }

    private ObjectResult Invalid() => Problem(statusCode: 400, title: "Unable to replace the authenticator. Check your password and new code. Start within five minutes of a two-factor sign-in and finish the setup within ten minutes.");
}

public sealed record ReplacementProof(Guid RequestId, [Required, StringLength(128)] string CurrentPassword,
    [Required, RegularExpression("^[0-9]{6}$")] string Code);
