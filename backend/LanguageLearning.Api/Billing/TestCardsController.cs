using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Billing;

[ApiController]
[Authorize]
[Route("api/v1/me")]
[EnableRateLimiting("learning")]
[RequestSizeLimit(2048)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TestCardsController(TestCardService cards, TestCardQueries queries) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("test-cards")]
    [ProducesResponseType<IReadOnlyList<TestCardView>>(200)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await queries.ListAsync(UserId, cancellationToken));

    [HttpPost("test-cards")]
    [ProducesResponseType<CardChange>(200)]
    public async Task<IActionResult> Create(CreateTestCard request, CancellationToken cancellationToken) =>
        Result(await cards.CreateAsync(UserId, FamilyId, request.Nickname, request.Currency, request.IdempotencyKey, cancellationToken));

    [HttpPatch("test-cards/{id:guid}")]
    [ProducesResponseType<CardChange>(200)]
    public async Task<IActionResult> Edit(Guid id, EditTestCard request, CancellationToken cancellationToken) =>
        Result(await cards.EditAsync(UserId, FamilyId, id, request.Nickname, request.MakeDefault, request.IdempotencyKey, cancellationToken));

    [HttpPost("test-cards/{id:guid}/block")]
    [ProducesResponseType<CardChange>(200)]
    public async Task<IActionResult> Block(Guid id, TestCardRequest request, CancellationToken cancellationToken) =>
        Result(await cards.StatusAsync(UserId, FamilyId, id, true, request.IdempotencyKey, cancellationToken));

    [HttpPost("test-cards/{id:guid}/unblock")]
    [ProducesResponseType<CardChange>(200)]
    public async Task<IActionResult> Unblock(Guid id, TestCardRequest request, CancellationToken cancellationToken) =>
        Result(await cards.StatusAsync(UserId, FamilyId, id, false, request.IdempotencyKey, cancellationToken));

    [HttpPost("test-cards/{id:guid}/top-ups")]
    [ProducesResponseType<CardChange>(200)]
    public async Task<IActionResult> TopUp(Guid id, TopUpTestCard request, CancellationToken cancellationToken) =>
        Result(await cards.TopUpAsync(UserId, FamilyId, id, request.Amount, request.IdempotencyKey, cancellationToken));

    [HttpGet("transactions")]
    [ProducesResponseType<BillingActivityPage>(200)]
    public Task<IActionResult> History([FromQuery] CardHistoryFilters filters, CancellationToken cancellationToken) =>
        HistoryAsync(null, filters, cancellationToken);

    [HttpGet("test-cards/{id:guid}/transactions")]
    [ProducesResponseType<BillingActivityPage>(200)]
    public Task<IActionResult> CardHistory(Guid id, [FromQuery] CardHistoryFilters filters, CancellationToken cancellationToken) =>
        HistoryAsync(id, filters, cancellationToken);

    private async Task<IActionResult> HistoryAsync(Guid? id, CardHistoryFilters filters, CancellationToken cancellationToken)
    {
        if (id.HasValue && filters.CardId.HasValue && id != filters.CardId || filters.From >= filters.To)
            return Problem(statusCode: 400, title: "Use one card and a valid date range.");
        var history = await queries.HistoryAsync(UserId, id ?? filters.CardId, filters.Type, filters.Status, filters.From, filters.To, filters.Page, cancellationToken);
        return history is null ? NotFound() : Ok(history);
    }

    private IActionResult Result(CardOutcome result) => result.Missing ? NotFound()
        : result.Error is { } error ? Problem(statusCode: 409, title: error) : Ok(result.Value);
}

public sealed record TestCardRequest(Guid IdempotencyKey);
public sealed record CreateTestCard([Required, StringLength(40, MinimumLength = 1)] string Nickname,
    [Required, RegularExpression("^(GEL|USD|EUR)$")] string Currency, Guid IdempotencyKey);
public sealed record EditTestCard([Required, StringLength(40, MinimumLength = 1)] string Nickname, bool MakeDefault, Guid IdempotencyKey);
public sealed record TopUpTestCard(decimal Amount, Guid IdempotencyKey);
public sealed class CardHistoryFilters
{
    public Guid? CardId { get; init; }
    [RegularExpression("^(TopUp|Debit|Refund)$")] public string? Type { get; init; }
    [RegularExpression("^(Succeeded|Failed|Pending)$")] public string? Status { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    [Range(1, 100000)] public int Page { get; init; } = 1;
}
