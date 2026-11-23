using System.ComponentModel.DataAnnotations;
using LanguageLearning.Api.Content;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Vocabulary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Vocabulary;

[ApiController]
[Authorize(Roles = "ContentAuthor,Reviewer,Admin,SuperAdmin")]
[Route("api/v1/manage/vocabulary/revisions")]
[EnableRateLimiting("content-write")]
[RequestSizeLimit(16384)]
public sealed class VocabularyRevisionsController(VocabularyRevisions revisions) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<VocabularyRevisionView[]>(200)]
    public async Task<IActionResult> List([FromQuery, Required] Guid? courseId,
        [FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await revisions.ListAsync(ActorId, FamilyId, courseId!.Value, page, cancellationToken);
        return result is null ? Forbid() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<ContentCreatedResponse>(201)]
    public async Task<IActionResult> Create(CreateVocabularyRevision request, CancellationToken cancellationToken) =>
        Result(await revisions.CreateAsync(ActorId, FamilyId, request.CourseId, request.WordId, request.Text,
            HttpContext.TraceIdentifier, cancellationToken), created: true);

    [HttpGet("{id:guid}")]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<VocabularyRevisionView>(200)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await revisions.GetAsync(ActorId, FamilyId, id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Change(Guid id, ChangeVocabularyRevision request, CancellationToken cancellationToken) =>
        Result(await revisions.ChangeAsync(ActorId, FamilyId, id, request.RevisionToken, request.Action,
            request.Text, request.Comment, HttpContext.TraceIdentifier, cancellationToken));

    private IActionResult Result(ContentOutcome result, bool created = false)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (result.Forbidden) return Forbid();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        var response = new ContentCreatedResponse(result.Id!.Value);
        return created ? CreatedAtAction(nameof(Get), new { id = result.Id }, response) : Ok(response);
    }
}

public sealed record CreateVocabularyRevision(Guid CourseId, Guid? WordId, [Required] VocabularyText Text);
public sealed record ChangeVocabularyRevision(Guid RevisionToken,
    [Required, RegularExpression("^(edit|submit|approve|request-changes|publish|withdraw)$")] string Action,
    VocabularyText? Text,
    [StringLength(1000), RegularExpression(@"^[^\u0000]*$")] string? Comment);
