using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1")]
[RequestSizeLimit(1024)]
[EnableRateLimiting("learning")]
public sealed class PlacementController(PlacementService placement) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpPost("courses/{id:guid}/placement-sessions")]
    [ProducesResponseType<SessionView>(200)]
    [ProducesResponseType<SessionView>(201)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        Result(await placement.StartAsync(UserId, FamilyId, id, cancellationToken));

    [HttpGet("me/placement-sessions/{id:guid}")]
    [ProducesResponseType<PlacementResult>(200)]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await placement.ResultAsync(UserId, id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("me/placement-sessions/{id:guid}/apply")]
    [ProducesResponseType<PlacementResult>(200)]
    public async Task<IActionResult> Apply(Guid id, CancellationToken cancellationToken) =>
        Result(await placement.ApplyAsync(UserId, FamilyId, id, cancellationToken));

    private IActionResult Result(LearningOutcome outcome)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (outcome.Missing) return NotFound();
        if (outcome.Error is { } error) return Problem(statusCode: 409, title: error);
        return outcome.Created && outcome.Value is SessionView session
            ? CreatedAtRoute(LearningController.SessionDetailRoute, new { id = session.Id }, session)
            : Ok(outcome.Value);
    }
}
