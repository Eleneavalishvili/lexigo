using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Billing;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/v1/admin")]
[EnableRateLimiting("session-refresh")]
[RequestSizeLimit(4096)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BillingAdministrationController(BillingAdministrationAccess access, BillingAdministrationQueries queries, RefundService refunds) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("payments")]
    public async Task<IActionResult> Payments([FromQuery] BillingAdminFilters filters, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(ActorId, FamilyId, cancellationToken)) return Forbid();
        if (filters.From >= filters.To || filters.Status is not (null or "Succeeded" or "Failed" or "Pending")) return BadRequest();
        return Ok(await queries.PaymentsAsync(filters.UserId, filters.PaymentId, filters.Status, filters.From, filters.To, filters.Page, cancellationToken));
    }
    [HttpGet("subscriptions")]
    public async Task<IActionResult> Subscriptions([FromQuery] BillingAdminFilters filters, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(ActorId, FamilyId, cancellationToken)) return Forbid();
        if (filters.Status is not (null or "Active" or "Expired" or "Refunded")) return BadRequest();
        return Ok(await queries.SubscriptionsAsync(filters.UserId, filters.Status, filters.Page, cancellationToken));
    }
    [HttpGet("billing-audit")]
    public async Task<IActionResult> Audit([FromQuery] BillingAdminFilters filters, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(ActorId, FamilyId, cancellationToken)) return Forbid();
        if (filters.From >= filters.To) return BadRequest();
        return Ok(await queries.AuditAsync(filters.From, filters.To, filters.Page, cancellationToken));
    }
    [HttpPost("payments/{id:guid}/refunds")]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> Refund(Guid id, RefundRequest request, CancellationToken cancellationToken) =>
        Result(await refunds.RefundAsync(ActorId, FamilyId, id, request.Amount, request.Reason, request.CurrentPassword, request.IdempotencyKey, cancellationToken));
    [HttpGet("refunds/by-request/{key:guid}")]
    public async Task<IActionResult> RefundByRequest(Guid key, CancellationToken cancellationToken) => Result(await refunds.ByRequestAsync(ActorId, FamilyId, key, cancellationToken));
    private IActionResult Result(RefundOutcome outcome) => outcome.Forbidden ? Forbid() : outcome.Missing ? NotFound()
        : outcome.Error is { } error ? Problem(statusCode: 409, title: error) : Ok(outcome.Value);
}
public sealed class BillingAdminFilters
{
    public Guid? UserId { get; init; }
    public Guid? PaymentId { get; init; }
    [StringLength(12)] public string? Status { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    [Range(1, 100000)] public int Page { get; init; } = 1;
}
public sealed record RefundRequest(decimal Amount, [Required, StringLength(500, MinimumLength = 3)] string Reason,
    [Required, StringLength(128)] string CurrentPassword, Guid IdempotencyKey);
