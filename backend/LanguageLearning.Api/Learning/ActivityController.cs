using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1/me/activity")]
[EnableRateLimiting("learning")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ActivityController(ActivityQueries activity) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ActivityPage>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Get([FromQuery, BindRequired] DateOnly from, [FromQuery, BindRequired] DateOnly to,
        [FromQuery, Range(1, 10000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 31,
        CancellationToken cancellationToken = default)
    {
        if (to < from || to.DayNumber - from.DayNumber >= 366)
            return Problem(statusCode: 400, title: "Choose an inclusive date range of at most 366 days.");

        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        return Ok(await activity.ListAsync(userId, from, to, page, pageSize, cancellationToken));
    }
}
