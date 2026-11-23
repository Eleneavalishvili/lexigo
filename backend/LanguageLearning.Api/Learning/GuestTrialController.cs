using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[AllowAnonymous]
[Route("api/v1/courses/{courseId:guid}/trial")]
[EnableRateLimiting("content-read")]
[RequestSizeLimit(1024)]
public sealed class GuestTrialController(GuestTrialService trials) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<GuestTrial>(200)]
    public async Task<IActionResult> Read(Guid courseId, CancellationToken cancellationToken)
    {
        Private();
        var trial = await trials.ReadAsync(courseId, cancellationToken);
        return trial is null ? NotFound() : Ok(trial);
    }

    [HttpPost("answers")]
    [ProducesResponseType<TrialFeedback>(200)]
    public async Task<IActionResult> Check(Guid courseId, TrialAnswer input, CancellationToken cancellationToken)
    {
        Private();
        if (input.VersionId == Guid.Empty || input.QuestionId == Guid.Empty || input.OptionId == Guid.Empty)
            return BadRequest();
        var feedback = await trials.CheckAsync(courseId, input, cancellationToken);
        return feedback is null ? NotFound() : Ok(feedback);
    }

    private void Private() => Response.Headers.CacheControl = "private, no-store";
}
