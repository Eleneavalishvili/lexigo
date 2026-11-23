using System.ComponentModel.DataAnnotations;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Administration;

[ApiController]
[Authorize(Roles = RoleNames.SuperAdmin)]
[Route("api/v1/admin/users/{id:guid}/billing-permission")]
[EnableRateLimiting("account-entry")]
[RequestSizeLimit(4096)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BillingPermissionsController(BillingPermissionService permissions) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => Result(await permissions.GetAsync(ActorId, FamilyId, id, cancellationToken));
    [HttpPut]
    public async Task<IActionResult> Set(Guid id, BillingPermissionRequest request, CancellationToken cancellationToken) =>
        Result(await permissions.SetAsync(ActorId, FamilyId, id, request.Enabled, request.Reason, request.CurrentPassword, request.IdempotencyKey, cancellationToken));

    private IActionResult Result(BillingPermissionOutcome outcome) => outcome.Forbidden ? Forbid() : outcome.Missing ? NotFound()
        : outcome.Error is { } error ? Problem(statusCode: 409, title: error) : Ok(outcome.Value);
}
public sealed record BillingPermissionRequest(bool Enabled, [Required, StringLength(500, MinimumLength = 3)] string Reason,
    [Required, StringLength(128)] string CurrentPassword, Guid IdempotencyKey);
