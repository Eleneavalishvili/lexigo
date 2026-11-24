using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[Authorize(Roles = "ContentAuthor,Reviewer,Admin,SuperAdmin")]
[Route("api/v1/manage/dialogues")]
[RequestSizeLimit(131072)]
[EnableRateLimiting("content-write")]
public sealed class ManagedDialoguesController(DialogueDrafts drafts, DialogueWorkflow workflow) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [EnableRateLimiting("content-read")]
    public async Task<IActionResult> List([FromQuery] Guid courseId, [FromQuery, Range(1, 100000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken token = default)
    {
        var rows = await drafts.ListAsync(ActorId, FamilyId, courseId, page, pageSize, token);
        return rows is null ? NotFound() : Ok(rows);
    }

    [HttpGet("{id:guid}")]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<StaffDialogue>(200)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken token)
    {
        var result = await drafts.DetailAsync(ActorId, FamilyId, id, token);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDialogueInput input, CancellationToken token) =>
        Write(await drafts.CreateAsync(ActorId, FamilyId, input, HttpContext.TraceIdentifier, token), true);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Save(Guid id, DialogueDraftInput input, CancellationToken token) =>
        Write(await drafts.SaveAsync(ActorId, FamilyId, id, input, HttpContext.TraceIdentifier, token));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, DialogueTransition input, CancellationToken token) => Transition(id, "submit", input, token);

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, DialogueTransition input, CancellationToken token) => Transition(id, "approve", input, token);

    [HttpPost("{id:guid}/return")]
    public Task<IActionResult> Return(Guid id, DialogueTransition input, CancellationToken token) => Transition(id, "return", input, token);

    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, DialogueTransition input, CancellationToken token) => Transition(id, "publish", input, token);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, DialogueTransition input, CancellationToken token)
    {
        var result = await workflow.TransitionAsync(ActorId, FamilyId, id, "archive", input, HttpContext.TraceIdentifier, token);
        return result.Error is null && !result.Forbidden ? NoContent() : Write(result);
    }

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> Revision(Guid id, CancellationToken token) =>
        Write(await workflow.RevisionAsync(ActorId, FamilyId, id, HttpContext.TraceIdentifier, token), true);

    private async Task<IActionResult> Transition(Guid id, string operation, DialogueTransition input, CancellationToken token) =>
        Write(await workflow.TransitionAsync(ActorId, FamilyId, id, operation, input, HttpContext.TraceIdentifier, token));

    private IActionResult Write(ContentOutcome result, bool created = false)
    {
        if (result.Forbidden) return Forbid();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        return created ? CreatedAtAction(nameof(Detail), new { id = result.Id }, new { id = result.Id }) : Ok(new { id = result.Id });
    }
}
