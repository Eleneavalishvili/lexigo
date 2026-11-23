using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1")]
[EnableRateLimiting("learning")]
[RequestSizeLimit(1024)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(401)]
public sealed class DialoguesController(DialogueService dialogues) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("dialogues")]
    [ProducesResponseType<DialogueListing[]>(200)]
    public async Task<IActionResult> List([FromQuery] Guid courseId, CancellationToken cancellationToken) =>
        Ok(await dialogues.ListAsync(UserId, courseId, cancellationToken));

    [HttpPost("dialogues/{id:guid}/sessions")]
    [ProducesResponseType<DialogueView>(200)]
    [ProducesResponseType<DialogueView>(201)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        Result(await dialogues.StartAsync(UserId, FamilyId, id, cancellationToken));

    [HttpGet("me/dialogue-sessions/{id:guid}")]
    [ProducesResponseType<DialogueView>(200)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken) =>
        Result(await dialogues.ReadAsync(UserId, id, cancellationToken));

    [HttpPost("me/dialogue-sessions/{id:guid}/choices")]
    [ProducesResponseType<DialogueView>(200)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Choose(Guid id, DialogueChoiceRequest request, CancellationToken cancellationToken) =>
        Result(await dialogues.ChooseAsync(UserId, FamilyId, id, request.ChoiceId, request.IdempotencyKey, cancellationToken));

    private IActionResult Result(DialogueOutcome outcome)
    {
        if (outcome.Missing) return NotFound();
        if (outcome.PremiumRequired) return Problem(statusCode: 403, title: "This conversation requires Premium.");
        if (outcome.Error is { } error) return Problem(statusCode: 409, title: error);
        return outcome.Created
            ? CreatedAtAction(nameof(Read), new { id = outcome.Session!.Id }, outcome.Session)
            : Ok(outcome.Session);
    }
}

public sealed record DialogueChoiceRequest(Guid ChoiceId, Guid IdempotencyKey);
