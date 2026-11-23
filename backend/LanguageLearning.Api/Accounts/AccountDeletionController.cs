using System.ComponentModel.DataAnnotations;
using LanguageLearning.Api.Authentication;
using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Authorize]
[Route("api/v1/me")]
[EnableRateLimiting("account-entry")]
[RequestSizeLimit(2048)]
public sealed class AccountDeletionController(AccountDeletionService deletion, AccountDeletionResponsibilities responsibilities,
    RefreshCookie cookie) : ControllerBase
{
    [HttpGet("deletion-responsibilities")]
    [ProducesResponseType<DeletionResponsibility[]>(200)]
    public async Task<IActionResult> Responsibilities(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var pending = await responsibilities.ForSessionAsync(Guid.Parse(User.FindFirst("sub")!.Value),
            Guid.Parse(User.FindFirst("sid")!.Value), cancellationToken);
        return pending is null ? Unauthorized() : Ok(pending);
    }

    [HttpGet("deletion-verification")]
    public IActionResult Verification([FromServices] IAntiforgery antiforgery)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    [HttpDelete]
    [RequireCsrf]
    public async Task<IActionResult> Delete(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        if (request.Confirmation != "DELETE") return BadRequest();
        string? error = await deletion.DeleteAsync(Guid.Parse(User.FindFirst("sub")!.Value),
            Guid.Parse(User.FindFirst("sid")!.Value), request.CurrentPassword, request.AuthenticatorCode,
            request.RecoveryCode, HttpContext.TraceIdentifier, cancellationToken);
        if (error is not null) return Problem(statusCode: 409, title: error);
        cookie.Clear(Response);
        return NoContent();
    }
}

public sealed record DeleteAccountRequest(
    [Required, StringLength(128)] string CurrentPassword,
    [Required, StringLength(6)] string Confirmation,
    [StringLength(32)] string? AuthenticatorCode,
    [StringLength(32)] string? RecoveryCode);
