using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Billing;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/v1/admin/plans")]
[EnableRateLimiting("session-refresh")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BillingPlansAdministrationController(BillingAdministrationAccess access, BillingPlanQueries plans) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(4096)]
    public Task<IActionResult> Create(PlanCreateRequest request, [FromServices] BillingPlanEditor editor,
        CancellationToken cancellationToken) => Save(editor, new(null, request.Code, request.Name, null,
            request.Reason, request.CurrentPassword, request.IdempotencyKey), cancellationToken);

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(8192)]
    public Task<IActionResult> Rename(Guid id, PlanRenameRequest request, [FromServices] BillingPlanEditor editor,
        CancellationToken cancellationToken) => Save(editor, new(id, null, request.Name, request.ExpectedName,
            request.Reason, request.CurrentPassword, request.IdempotencyKey, request.Features, request.ExpectedFeatures,
            request.IsActive, request.ExpectedIsActive), cancellationToken);

    private async Task<IActionResult> Save(BillingPlanEditor editor, BillingPlanEdit edit, CancellationToken cancellationToken)
    {
        var outcome = await editor.SaveAsync(Guid.Parse(User.FindFirst("sub")!.Value),
            Guid.Parse(User.FindFirst("sid")!.Value), edit, cancellationToken);
        if (outcome.Forbidden) return Forbid();
        if (outcome.Missing) return NotFound();
        return outcome.Error is { } error ? Problem(statusCode: 409, title: error) : Ok(outcome.Plan);
    }

    [HttpGet]
    public async Task<IActionResult> Read([FromQuery] PlanCatalogueFilters filters, CancellationToken cancellationToken)
    {
        var actor = Guid.Parse(User.FindFirst("sub")!.Value);
        var family = Guid.Parse(User.FindFirst("sid")!.Value);
        if (!await access.AllowedAsync(actor, family, cancellationToken)) return Forbid();
        return Ok(await plans.ReadAsync(filters.Active, filters.Page, cancellationToken));
    }

    [HttpPost("prices/{priceId:guid}/schedule")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Schedule(Guid priceId, PriceScheduleRequest request,
        [FromServices] PlanPriceScheduling scheduling, CancellationToken cancellationToken)
    {
        var outcome = await scheduling.ScheduleAsync(Guid.Parse(User.FindFirst("sub")!.Value),
            Guid.Parse(User.FindFirst("sid")!.Value), priceId, request.Amount, request.StartsAt,
            request.Reason, request.CurrentPassword, request.IdempotencyKey, cancellationToken);
        if (outcome.Forbidden) return Forbid();
        if (outcome.Missing) return NotFound();
        return outcome.Error is { } error ? Problem(statusCode: 409, title: error) : Ok(outcome.Price);
    }

    [HttpGet("features")]
    public async Task<IActionResult> Features(CancellationToken cancellationToken)
    {
        var actor = Guid.Parse(User.FindFirst("sub")!.Value);
        var family = Guid.Parse(User.FindFirst("sid")!.Value);
        if (!await access.AllowedAsync(actor, family, cancellationToken)) return Forbid();
        return Ok(await plans.FeaturesAsync(cancellationToken));
    }
}

public sealed record PlanCreateRequest([Required, StringLength(24)] string Code, [Required, StringLength(80)] string Name,
    [Required, StringLength(500)] string Reason, [Required, StringLength(128)] string CurrentPassword, Guid IdempotencyKey);

public sealed record PlanRenameRequest([Required, StringLength(80)] string Name, [Required, StringLength(80)] string ExpectedName,
    [Required, StringLength(500)] string Reason, [Required, StringLength(128)] string CurrentPassword, Guid IdempotencyKey,
    [MaxLength(50)] PlanFeatureEdit[]? Features = null, [MaxLength(50)] PlanFeatureEdit[]? ExpectedFeatures = null,
    bool? IsActive = null, bool? ExpectedIsActive = null);

public sealed record PriceScheduleRequest(decimal Amount, DateTimeOffset StartsAt,
    [Required, StringLength(500)] string Reason, [Required, StringLength(128)] string CurrentPassword,
    Guid IdempotencyKey);

public sealed class PlanCatalogueFilters
{
    public bool? Active { get; init; }
    [Range(1, 100000)] public int Page { get; init; } = 1;
}
