using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Vocabulary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Vocabulary;

[ApiController]
[Authorize]
[Route("api/v1/me/collections")]
[EnableRateLimiting("learning")]
[RequestSizeLimit(1024)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WordCollectionsController(WordCollections collections) : ControllerBase
{
    public sealed record CollectionWriteResponse(Guid Id);

    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [ProducesResponseType<WordCollectionView[]>(200)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await collections.ListAsync(UserId, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WordCollectionView>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        await collections.GetAsync(UserId, id, cancellationToken) is { } collection ? Ok(collection) : NotFound();

    [HttpPost]
    [ProducesResponseType<CollectionWriteResponse>(200)]
    [ProducesResponseType<CollectionWriteResponse>(201)]
    public async Task<IActionResult> Create(CreateWordCollection request, CancellationToken cancellationToken) =>
        Write(await collections.SaveAsync(UserId, FamilyId, null, request.CourseId, request.Name, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameWordCollection request, CancellationToken cancellationToken) =>
        Write(await collections.SaveAsync(UserId, FamilyId, id, null, request.Name, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await collections.DeleteAsync(UserId, FamilyId, id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/words/{wordId:guid}")]
    public async Task<IActionResult> Add(Guid id, Guid wordId, CancellationToken cancellationToken) =>
        await collections.SetWordAsync(UserId, FamilyId, id, wordId, true, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}/words/{wordId:guid}")]
    public async Task<IActionResult> Remove(Guid id, Guid wordId, CancellationToken cancellationToken) =>
        await collections.SetWordAsync(UserId, FamilyId, id, wordId, false, cancellationToken) ? NoContent() : NotFound();

    private IActionResult Write(WordCollectionOutcome result)
    {
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        if (result.Id is not { } id) return NotFound();
        var response = new CollectionWriteResponse(id);
        return result.Created ? CreatedAtAction(nameof(Get), new { id }, response) : Ok(response);
    }
}

public sealed record CreateWordCollection(Guid CourseId, [Required, StringLength(80)] string Name);
public sealed record RenameWordCollection([Required, StringLength(80)] string Name);
