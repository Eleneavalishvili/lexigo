using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Billing;

[ApiController]
[Authorize]
[Route("api/v1/me/subscriptions")]
[EnableRateLimiting("learning")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SubscriptionHistoryController(SubscriptionHistoryQueries history) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<ActionResult<SubscriptionHistoryPage>> List(CancellationToken cancellationToken, [Range(1, 100000)] int page = 1) =>
        Ok(await history.ListAsync(UserId, page, cancellationToken));

    [HttpGet("{id:guid}/events")]
    public async Task<ActionResult<SubscriptionEventsPage>> Events(Guid id, CancellationToken cancellationToken, [Range(1, 100000)] int page = 1)
    {
        var value = await history.EventsAsync(UserId, id, page, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }
}
