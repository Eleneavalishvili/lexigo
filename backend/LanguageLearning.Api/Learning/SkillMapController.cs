using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1/me/courses/{courseId:guid}/skills")]
[EnableRateLimiting("learning")]
public sealed class SkillMapController(SkillMapQueries skills) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SkillMapEntry[]>(200)]
    public async Task<IActionResult> Get(Guid courseId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var map = await skills.ReadAsync(userId, courseId, cancellationToken);
        return map is null ? NotFound() : Ok(map);
    }
}
