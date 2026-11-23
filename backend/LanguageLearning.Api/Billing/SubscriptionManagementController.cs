using LanguageLearning.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Billing;

[ApiController]
[Authorize]
[Route("api/v1/me/subscription")]
[EnableRateLimiting("learning")]
[RequestSizeLimit(2048)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SubscriptionManagementController(SubscriptionManagementService management) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);
    [HttpPost("cancel")]
    [ProducesResponseType<SubscriptionView>(200)]
    public async Task<IActionResult> Cancel(SubscriptionAction request, CancellationToken cancellationToken) => Result(await management.CancelAsync(UserId, FamilyId, request.SubscriptionId, request.IdempotencyKey, true, cancellationToken));
    [HttpPost("resume")]
    [ProducesResponseType<SubscriptionView>(200)]
    public async Task<IActionResult> Resume(SubscriptionAction request, CancellationToken cancellationToken) => Result(await management.CancelAsync(UserId, FamilyId, request.SubscriptionId, request.IdempotencyKey, false, cancellationToken));
    [HttpPut("renewal")]
    [ProducesResponseType<SubscriptionView>(200)]
    public async Task<IActionResult> Renewal(SubscriptionRenewal request, CancellationToken cancellationToken) => Result(await management.RenewalAsync(UserId, FamilyId, request.SubscriptionId, request.IdempotencyKey, request.AutoRenew, request.DefaultTestCardId, cancellationToken));
    [HttpPut("scheduled-plan")]
    [ProducesResponseType<SubscriptionView>(200)]
    public async Task<IActionResult> Schedule(ScheduledSubscriptionPlan request, CancellationToken cancellationToken) => Result(await management.ScheduleAsync(UserId, FamilyId, request.SubscriptionId, request.IdempotencyKey, request.PlanPriceId, cancellationToken));
    private IActionResult Result(SubscriptionChangeOutcome result)
    {
        if (result.RateLimited) { Response.Headers.RetryAfter = "600"; return Problem(statusCode: 429, title: "Take a short break before changing more preferences."); }
        return result.Missing ? NotFound() : result.Error is { } error ? Problem(statusCode: 409, title: error) : Ok(result.Value);
    }
}
public sealed record SubscriptionAction(Guid SubscriptionId, Guid IdempotencyKey);
public sealed record SubscriptionRenewal(Guid SubscriptionId, bool AutoRenew, Guid? DefaultTestCardId, Guid IdempotencyKey);
public sealed record ScheduledSubscriptionPlan(Guid SubscriptionId, Guid? PlanPriceId, Guid IdempotencyKey);
