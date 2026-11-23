using LanguageLearning.Api.Authentication;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class CurrentAccountController(AccountQueryService accounts, SessionService sessions, PasswordRecoveryService recovery, RefreshCookie cookie) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [ProducesResponseType<AccountSummary>(200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Current()
    {
        var account = await accounts.CurrentAsync(UserId);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken) => Ok(await accounts.SessionsAsync(UserId, FamilyId, cancellationToken));

    [HttpGet("sessions/{id:guid}")]
    public async Task<IActionResult> Session(Guid id, CancellationToken cancellationToken)
    {
        var session = (await accounts.SessionsAsync(UserId, FamilyId, cancellationToken)).SingleOrDefault(item => item.Id == id);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        if (!await sessions.RevokeOwnedAsync(UserId, id, cancellationToken)) return NotFound();
        if (id == FamilyId) cookie.Clear(Response);
        return NoContent();
    }

    [HttpDelete("sessions")]
    public async Task<IActionResult> RevokeAll(CancellationToken cancellationToken)
    {
        await sessions.RevokeAllAsync(UserId, cancellationToken);
        cookie.Clear(Response);
        return NoContent();
    }

    [HttpPut("password")]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!await recovery.ChangeAsync(UserId, request.CurrentPassword, request.NewPassword, cancellationToken))
            return Problem(statusCode: 400, title: "Unable to change your password. Check your current password and try again.");
        cookie.Clear(Response);
        return Ok(new { message = "Password changed. Sign in again on your devices." });
    }
}
