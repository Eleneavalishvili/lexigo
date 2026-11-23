using LanguageLearning.Infrastructure.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[Authorize(Roles = "ContentAuthor,Reviewer,Admin,SuperAdmin")]
[Route("api/v1/manage")]
[RequestSizeLimit(65536)]
[EnableRateLimiting("content-write")]
public sealed class ManagedExercisesController(ManagedExerciseQueries queries, DraftExerciseService exercises) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("exercises/{id:guid}")]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<ManagedExercise>(200)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await queries.GetAsync(ActorId, FamilyId, id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("lessons/{id:guid}/exercises")]
    [ProducesResponseType<ContentCreatedResponse>(201)]
    public async Task<IActionResult> Create(Guid id, ExerciseCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await exercises.SaveAsync(ActorId, FamilyId, id, request.VersionId, null,
            Input(request), HttpContext.TraceIdentifier, cancellationToken);
        if (result.Forbidden || result.Error is not null) return Write(result);
        Response.Headers.CacheControl = "private, no-store";
        return CreatedAtAction(nameof(Get), new { id = result.Id }, new ContentCreatedResponse(result.Id!.Value));
    }

    [HttpPut("lessons/{id:guid}/exercises/{exerciseId:guid}")]
    [ProducesResponseType<ContentCreatedResponse>(200)]
    public async Task<IActionResult> Edit(Guid id, Guid exerciseId, ExerciseCreateRequest request, CancellationToken cancellationToken) =>
        Write(await exercises.SaveAsync(ActorId, FamilyId, id, request.VersionId, exerciseId,
            Input(request), HttpContext.TraceIdentifier, cancellationToken));

    [HttpPost("lessons/{id:guid}/exercises/{exerciseId:guid}/move")]
    public async Task<IActionResult> Move(Guid id, Guid exerciseId, MoveExerciseRequest request, CancellationToken cancellationToken) =>
        Write(await exercises.MoveAsync(ActorId, FamilyId, id, request.VersionId, exerciseId,
            request.NeighborId, request.Earlier, HttpContext.TraceIdentifier, cancellationToken));

    [HttpDelete("lessons/{id:guid}/exercises/{exerciseId:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Remove(Guid id, Guid exerciseId, VersionRequest request, CancellationToken cancellationToken) =>
        Write(await exercises.RemoveAsync(ActorId, FamilyId, id, exerciseId, request.VersionId,
            HttpContext.TraceIdentifier, cancellationToken), noContent: true);

    private IActionResult Write(ContentOutcome result, bool noContent = false)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (result.Forbidden) return Forbid();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        return noContent ? NoContent() : Ok(new ContentCreatedResponse(result.Id!.Value));
    }

    private static ExerciseInput Input(ExerciseCreateRequest request) => new(request.Type, request.Prompt,
        request.Explanation, request.OrderNumber, request.Options, request.Answers, request.IgnoreCase,
        request.IgnoreTerminalPunctuation, request.TokenOrders, request.MatchPairs, request.ContextText,
        request.AudioMediaId, request.SkillIds);
}
