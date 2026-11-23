using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1/me/weekly-summary")]
[EnableRateLimiting("learning")]
public sealed class WeeklySummaryController(WeeklySummaryQueries summaries) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WeeklySummaryView>(200)]
    public async Task<IActionResult> Get([FromQuery, Range(0, 12)] int weeksAgo = 0, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        var summary = await summaries.GetAsync(Guid.Parse(User.FindFirst("sub")!.Value), weeksAgo, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("comparison")]
    [ProducesResponseType<WeeklyComparison>(200)]
    public async Task<IActionResult> Compare([FromServices] SubscriptionQueries subscriptions,
        [FromQuery, Range(0, 11)] int weeksAgo = 0, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        if (!await subscriptions.HasAsync(userId, "AdvancedAnalytics", cancellationToken))
            return Problem(statusCode: 403, title: "Week comparison requires Premium.");

        var comparison = await summaries.CompareAsync(userId, weeksAgo, cancellationToken);
        return comparison is null ? NotFound() : Ok(comparison);
    }
}
