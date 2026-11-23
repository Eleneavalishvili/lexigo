using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1/me/challenges")]
[EnableRateLimiting("learning")]
public sealed class ChallengesController(ChallengeQueries queries) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ChallengeView[]>(200)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await queries.GetAsync(Guid.Parse(User.FindFirst("sub")!.Value), cancellationToken));
    }
}
