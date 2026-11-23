using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[AllowAnonymous]
[Route("api/v1/completion-cards/shared")]
[EnableRateLimiting("content-read")]
public sealed class SharedCompletionCardsController(CompletionCardQueries cards) : ControllerBase
{
    [HttpGet("{code}")]
    [ProducesResponseType<CompletionCardDetails>(200)]
    public async Task<IActionResult> Get(string code, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var card = await cards.SharedAsync(code, cancellationToken);
        return card is null ? NotFound() : Ok(card);
    }
}
