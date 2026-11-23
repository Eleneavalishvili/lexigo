using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1/me/dashboard")]
[EnableRateLimiting("learning")]
public sealed class DashboardController(DashboardQueries dashboard) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardView>(200)]
    public async Task<IActionResult> Get([FromQuery] Guid? courseId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var value = await dashboard.GetAsync(Guid.Parse(User.FindFirst("sub")!.Value), courseId, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }
}
