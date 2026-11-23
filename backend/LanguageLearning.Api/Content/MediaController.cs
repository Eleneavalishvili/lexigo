using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[Authorize(Roles = "ContentAuthor,Reviewer,Admin,SuperAdmin")]
[Route("api/v1/manage/media")]
[EnableRateLimiting("content-read")]
public sealed class MediaController(MediaUploadService uploads, MediaManagementService management, MediaQueries queries) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await queries.ListAsync(ActorId, FamilyId, page, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<ContentCreatedResponse>(201)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10616832)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10616832, ValueLengthLimit = 2000, MultipartHeadersLengthLimit = 4096)]
    [EnableRateLimiting("media-upload")]
    public async Task<IActionResult> Upload([FromForm] AudioUploadRequest request, CancellationToken cancellationToken)
    {
        await using var stream = request.File.OpenReadStream();
        return Outcome(await uploads.UploadAsync(ActorId, FamilyId, new(request.LanguageId, request.Source, request.License),
            stream, HttpContext.TraceIdentifier, cancellationToken), created: true);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StaffMedia>(200)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var value = await queries.GetAsync(ActorId, FamilyId, id, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var url = await queries.PreviewAsync(ActorId, FamilyId, id, cancellationToken);
        return url is null ? NotFound() : Ok(new { url, expiresInSeconds = 120 });
    }

    [HttpPost("{id:guid}/reviews")]
    [RequestSizeLimit(8192)]
    [EnableRateLimiting("content-write")]
    public async Task<IActionResult> Review(Guid id, MediaReviewRequest request, CancellationToken cancellationToken) => Outcome(
        await management.ReviewAsync(ActorId, FamilyId, id, request.Decision, request.Comment, HttpContext.TraceIdentifier, cancellationToken));

    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("content-write")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) => Outcome(
        await management.DeleteAsync(ActorId, FamilyId, id, HttpContext.TraceIdentifier, cancellationToken));

    [HttpPost("{id:guid}/finish-deletion")]
    [EnableRateLimiting("content-write")]
    public async Task<IActionResult> FinishDeletion(Guid id, CancellationToken cancellationToken) =>
        Outcome(await management.FinishDeletionAsync(ActorId, FamilyId, id, HttpContext.TraceIdentifier, cancellationToken));

    private IActionResult Outcome(MediaOutcome result, bool created = false)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (result.Forbidden) return Forbid();
        if (result.Unavailable) return Problem(statusCode: 503, title: "Audio storage is temporarily unavailable. Try again later.");
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        return created ? CreatedAtAction(nameof(Get), new { id = result.Id }, new ContentCreatedResponse(result.Id!.Value))
            : Ok(new { id = result.Id });
    }
}

public sealed class AudioUploadRequest
{
    [Required] public required IFormFile File { get; init; }
    [Range(1, int.MaxValue)] public int LanguageId { get; init; }
    [Required, MaxLength(2000)] public required string Source { get; init; }
    [Required, MaxLength(1000)] public required string License { get; init; }
}

public sealed record MediaReviewRequest([Required] string Decision, [Required, MaxLength(2000)] string Comment);
