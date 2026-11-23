using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Administration;

[ApiController]
[Authorize(Roles = RoleNames.SuperAdmin)]
[Route("api/v1/admin/settings")]
[RequestSizeLimit(8192)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting("content-write")]
public sealed class SystemSettingsController(SystemSettings settings) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid Family => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [ProducesResponseType<StudySettingsView>(200)]
    public async Task<IActionResult> Read(CancellationToken token) =>
        await settings.AdminReadAsync(Actor, Family, token) is { } view ? Ok(view) : Forbid();

    [HttpPut]
    public async Task<IActionResult> Update(UpdateStudySettings input, CancellationToken token)
    {
        var result = await settings.UpdateAsync(Actor, Family, input, HttpContext.TraceIdentifier, token);
        if (result.Forbidden) return Forbid();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        return NoContent();
    }
}
