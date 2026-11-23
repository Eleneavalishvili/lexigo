using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1")]
[EnableRateLimiting("learning")]
public sealed class LeaderboardController(LeaderboardQueries ranking, LeaderboardParticipationService participation) : ControllerBase
{
    [HttpGet("leaderboards")]
    [ProducesResponseType<LeaderboardView>(200)]
    public async Task<IActionResult> Get([FromQuery] Guid courseId, [FromQuery] DateOnly? week,
        [FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        DateOnly selected = week ?? ranking.CurrentWeek();
        if (selected.DayOfWeek != DayOfWeek.Monday || selected > ranking.CurrentWeek())
            return Problem(statusCode: 400, title: "Choose a Monday on or before the current UTC week.");
        var value = await ranking.GetAsync(Guid.Parse(User.FindFirst("sub")!.Value), courseId, selected, page, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPut("me/leaderboard-participation")]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> Participate(LeaderboardParticipationRequest request, CancellationToken cancellationToken)
    {
        if (request is { OptedIn: false, SharePhoto: true })
            return Problem(statusCode: 400, title: "Join the ranking before sharing a profile photo.");

        bool saved = await participation.SetAsync(Guid.Parse(User.FindFirst("sub")!.Value),
            Guid.Parse(User.FindFirst("sid")!.Value), request.OptedIn!.Value, request.SharePhoto, cancellationToken);
        return saved ? NoContent() : NotFound();
    }
}

public sealed record LeaderboardParticipationRequest([Required] bool? OptedIn, bool? SharePhoto = null);
