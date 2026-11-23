using System.ComponentModel.DataAnnotations;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Administration;

[ApiController]
[RequestSizeLimit(16384)]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/v1/admin")]
[EnableRateLimiting("session-refresh")]
public sealed class UsersController(AdministrationQueries queries, UserAdministrationService administration) : ControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType<AdministrationPage<ManagedAccountSummary>>(200)]
    public async Task<IActionResult> Users([FromQuery, StringLength(80)] string? search, [FromQuery] string? role, [FromQuery] string? status, [FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default)
    {
        if ((role is not null && !RoleNames.All.Contains(role)) || (status is not null && status is not ("Active" or "Blocked"))) return BadRequest();
        return Ok(await queries.UsersAsync(search, role, status, page, cancellationToken));
    }

    [HttpGet("languages")]
    [ProducesResponseType<LanguageAccessChoice[]>(200)]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<IActionResult> Languages(CancellationToken cancellationToken) => Ok(await queries.LanguagesAsync(cancellationToken));

    [HttpGet("audit-logs")]
    [ProducesResponseType<AdministrationPage<AuditSummary>>(200)]
    public async Task<IActionResult> Audit([FromQuery] Guid? userId, [FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default) =>
        Ok(await queries.AuditAsync(User.IsInRole(RoleNames.SuperAdmin), userId, page, cancellationToken));

    [HttpPatch("users/{id:guid}/status")]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> Status(Guid id, StatusRequest request, CancellationToken cancellationToken) => Result(await administration.UpdateAsync(
        Guid.Parse(User.FindFirst("sub")!.Value), Guid.Parse(User.FindFirst("sid")!.Value), id, request.CurrentPassword, request.Reason, HttpContext.TraceIdentifier, request.Status, null, null, cancellationToken));

    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> Roles(Guid id, RolesRequest request, CancellationToken cancellationToken) => Result(await administration.UpdateAsync(
        Guid.Parse(User.FindFirst("sub")!.Value), Guid.Parse(User.FindFirst("sid")!.Value), id, request.CurrentPassword, request.Reason, HttpContext.TraceIdentifier, null, request.Roles, request.LanguageIds, cancellationToken));

    private IActionResult Result(AdministrationResult result) => result switch
    {
        AdministrationResult.Success => NoContent(),
        AdministrationResult.Forbidden => Forbid(),
        AdministrationResult.NotFound => NotFound(),
        AdministrationResult.LastAdministrator => Problem(statusCode: 409, title: "The last active Super Admin must keep access."),
        AdministrationResult.EnrollmentRequired => Problem(statusCode: 400, title: "Privileged accounts must be active, email-confirmed and enrolled in two-factor authentication."),
        _ => Problem(statusCode: 400, title: "Check your details and current password. Changes require a two-factor sign-in within the last five minutes.")
    };
}

public sealed record StatusRequest([Required] string Status, [Required, StringLength(500)] string Reason, [Required, StringLength(128)] string CurrentPassword);
public sealed record RolesRequest([Required, MaxLength(7)] string[] Roles, [Required, MaxLength(4)] int[] LanguageIds, [Required, StringLength(500)] string Reason, [Required, StringLength(128)] string CurrentPassword);
