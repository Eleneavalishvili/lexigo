using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Billing;

[ApiController]
[Authorize]
[Route("api/v1/me/hearts")]
[EnableRateLimiting("learning")]
[RequestSizeLimit(1024)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HeartPurchasesController(HeartPurchaseService purchases) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("refill-offer")]
    [ProducesResponseType<HeartRefillOffer>(200)]
    public async Task<IActionResult> Offer(CancellationToken cancellationToken) => Ok(await purchases.OfferAsync(UserId, cancellationToken));

    [HttpPost("purchases")]
    [ProducesResponseType<HeartPurchaseView>(200)]
    public async Task<IActionResult> Purchase(HeartPurchaseRequest request, CancellationToken cancellationToken)
    {
        var result = await purchases.PurchaseAsync(UserId, FamilyId, request.TestCardId, request.OfferCode, request.IdempotencyKey, cancellationToken);
        if (result.RateLimited)
        {
            Response.Headers.RetryAfter = "600";
            return Problem(statusCode: 429, title: "Take a short break before starting more refill purchases.");
        }
        return result.Missing ? NotFound() : result.Error is { } error ? Problem(statusCode: 409, title: error) : Ok(result.Value);
    }

    [HttpGet("purchases/by-request/{key:guid}")]
    [ProducesResponseType<HeartPurchaseView>(200)]
    public async Task<IActionResult> ByRequest(Guid key, CancellationToken cancellationToken) =>
        await purchases.ByRequestAsync(UserId, key, cancellationToken) is { } value ? Ok(value) : NotFound();

    [HttpGet("purchases")]
    [ProducesResponseType<HeartPurchasePage>(200)]
    public async Task<IActionResult> History([FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default) =>
        Ok(await purchases.HistoryAsync(UserId, page, cancellationToken));
}

public sealed record HeartPurchaseRequest(Guid TestCardId, [Required, StringLength(50)] string OfferCode, Guid IdempotencyKey);
