using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Authorize]
[Route("api/v1/me/notifications")]
[EnableRateLimiting("learning")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<ActionResult<NotificationPage>> Page(CancellationToken cancellationToken, int limit = 30,
        DateTimeOffset? before = null, Guid? beforeId = null)
    {
        if (limit is < 1 or > 50 || before.HasValue != beforeId.HasValue)
            return Problem(statusCode: 400, title: "Use a page size of 1–50 and both cursor values together.");
        return Ok(await notifications.PageAsync(UserId, limit, before, beforeId, cancellationToken));
    }

    [HttpPatch("{id:guid}/read")]
    [RequestSizeLimit(1024)]
    public async Task<ActionResult<NotificationView>> Read(Guid id, CancellationToken cancellationToken)
    {
        var value = await notifications.ReadAsync(UserId, id, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }
}
