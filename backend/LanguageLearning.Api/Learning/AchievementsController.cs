using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1/me/achievements")]
[EnableRateLimiting("learning")]
public sealed class AchievementsController(AchievementQueries queries, AchievementAwards awards) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AchievementView[]>(200)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await queries.GetAsync(Guid.Parse(User.FindFirst("sub")!.Value), cancellationToken));
    }

    [HttpPost("collect")]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> Collect(CollectAchievementsRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var value = await awards.CollectAsync(Guid.Parse(User.FindFirst("sub")!.Value),
            Guid.Parse(User.FindFirst("sid")!.Value), cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }
}

public sealed record CollectAchievementsRequest;
