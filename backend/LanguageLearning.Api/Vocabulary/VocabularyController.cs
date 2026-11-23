using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Vocabulary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Vocabulary;

[ApiController]
[Authorize]
[Route("api/v1")]
[EnableRateLimiting("learning")]
[RequestSizeLimit(1024)]
public sealed class VocabularyController(VocabularyQueries queries, SavedWordService words,
    WordReviewService reviews, VocabularyAudio audio) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("vocabulary/{id:guid}/audio")]
    [ProducesResponseType<WordAudioLink>(200)]
    public async Task<IActionResult> Audio(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var outcome = await audio.PlaybackAsync(id, cancellationToken);
        if (outcome.Missing) return NotFound();
        if (outcome.Unavailable) return Problem(statusCode: 503, title: "Audio storage is unavailable.");
        return Ok(outcome.Value);
    }

    [HttpGet("vocabulary")]
    [ProducesResponseType<WordPage>(200)]
    public async Task<IActionResult> Catalog([FromQuery] WordFilters filters, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await queries.ListAsync(UserId, false, filters.CourseId, filters.Search, "all",
            filters.Page, cancellationToken, topic: filters.Topic));
    }

    [HttpGet("me/saved-words")]
    [ProducesResponseType<WordPage>(200)]
    public async Task<IActionResult> Saved([FromQuery] WordFilters filters, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await queries.ListAsync(UserId, true, filters.CourseId, filters.Search, filters.Status,
            filters.Page, cancellationToken, filters.CollectionId, filters.Topic));
    }

    [HttpPost("me/saved-words")]
    [ProducesResponseType<SaveWordResult>(200)]
    [ProducesResponseType<SaveWordResult>(201)]
    public async Task<IActionResult> Save(SaveWordRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var outcome = await words.SaveAsync(UserId, FamilyId, request.VocabularyItemId, cancellationToken);
        if (outcome.Error is { } error) return Problem(statusCode: 409, title: error);
        if (outcome.Word is not { } word) return NotFound();
        return outcome.Created ? CreatedAtAction(nameof(SavedDetail), new { id = word.Id }, word) : Ok(word);
    }

    [HttpGet("me/saved-words/{id:guid}")]
    [ProducesResponseType<SaveWordResult>(200)]
    public async Task<IActionResult> SavedDetail(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return await words.GetAsync(UserId, id, cancellationToken) is { } word ? Ok(word) : NotFound();
    }

    [HttpDelete("me/saved-words/{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken) =>
        await words.RemoveAsync(UserId, FamilyId, id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("me/saved-words/{id:guid}/reviews")]
    [ProducesResponseType<WordReviewResult>(200)]
    public async Task<IActionResult> Review(Guid id, ReviewWordRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.Answer))
            return BadRequest();
        var result = await reviews.ReviewAsync(UserId, FamilyId, id, request.IdempotencyKey, request.Answer, cancellationToken);
        return result.Missing ? NotFound() : result.Error is { } error
            ? Problem(statusCode: 409, title: error) : Ok(result.Value);
    }
}

public sealed class WordFilters
{
    public Guid? CourseId { get; init; }
    public Guid? CollectionId { get; init; }
    [StringLength(60), RegularExpression("^[a-z]+(-[a-z]+)*$")] public string? Topic { get; init; }
    [StringLength(160), RegularExpression(@"^[^\u0000]*$")] public string? Search { get; init; }
    [Range(1, 100000)] public int Page { get; init; } = 1;
    [RegularExpression("^(all|due|scheduled)$")] public string Status { get; init; } = "all";
}
public sealed record SaveWordRequest(Guid VocabularyItemId);

public sealed class ReviewWordRequest
{
    public Guid IdempotencyKey { get; init; }
    [Required, StringLength(160), RegularExpression(@"^[^\u0000]*$")]
    public string Answer { get; init; } = "";
}
