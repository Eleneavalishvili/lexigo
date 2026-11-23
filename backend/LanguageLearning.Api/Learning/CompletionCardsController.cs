using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1/me")]
[EnableRateLimiting("learning")]
[RequestSizeLimit(1024)]
public sealed class CompletionCardsController(CompletionCardQueries cards, CompletionCardManagement management) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("completion-cards")]
    [ProducesResponseType<CompletionCardPage>(200)]
    public async Task<IActionResult> List([FromQuery] Guid? unitId, [FromQuery, Range(1, 100000)] int page = 1,
        CancellationToken cancellationToken = default)
    {
        Private();
        return Ok(await cards.ListAsync(UserId, unitId, page, cancellationToken));
    }

    [HttpGet("completion-cards/{id:guid}")]
    [ProducesResponseType<OwnedCompletionCard>(200)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        Private();
        var card = await cards.GetAsync(UserId, id, cancellationToken);
        return card is null ? NotFound() : Ok(card);
    }

    [HttpPost("units/{unitId:guid}/completion-card")]
    [ProducesResponseType<CardClaimResponse>(200)]
    [ProducesResponseType<CardClaimResponse>(201)]
    public async Task<IActionResult> Claim(Guid unitId, CancellationToken cancellationToken)
    {
        Private();
        return Outcome(await management.ClaimAsync(UserId, FamilyId, unitId, cancellationToken));
    }

    [HttpPut("completion-cards/{id:guid}/sharing")]
    public async Task<IActionResult> Sharing(Guid id, CardSharingRequest request, CancellationToken cancellationToken)
    {
        Private();
        return Outcome(await management.SetSharingAsync(UserId, FamilyId, id, request.IsShared,
            request.ConfirmPublicName, request.Revision, HttpContext.TraceIdentifier, cancellationToken));
    }

    private void Private() => Response.Headers.CacheControl = "private, no-store";
    private IActionResult Outcome(CompletionCardOutcome result)
    {
        if (result.Missing) return NotFound();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        var response = new CardClaimResponse(result.Id!.Value);
        return result.Created ? CreatedAtAction(nameof(Get), new { id = response.Id }, response) : Ok(response);
    }

    public sealed record CardClaimResponse(Guid Id);
}

public sealed record CardSharingRequest(bool IsShared, bool ConfirmPublicName, [Range(1, int.MaxValue)] int Revision);
