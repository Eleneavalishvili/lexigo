using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[Authorize(Roles = "ContentAuthor,Reviewer,Admin,SuperAdmin")]
[Route("api/v1/manage/skills")]
[RequestSizeLimit(8192)]
[EnableRateLimiting("content-write")]
public sealed class ManagedSkillsController(SkillManagement management) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<ManagedSkillPage>(200)]
    public async Task<IActionResult> List([FromQuery] Guid courseId,
        [FromQuery, Range(1, 100000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20,
        [FromQuery, MaxLength(100)] string? search = null, [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await management.ListAsync(ActorId, FamilyId, courseId, page, pageSize, search, includeArchived, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:int}")]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<ManagedSkill>(200)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await management.GetAsync(ActorId, FamilyId, id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<ManagedSkill>(201)]
    public async Task<IActionResult> Create(SkillInput input, CancellationToken cancellationToken)
    {
        var result = await management.SaveAsync(ActorId, FamilyId, null, input, 0,
            "New course skill.", HttpContext.TraceIdentifier, cancellationToken);
        return Write(result, created: true);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ManagedSkill>(200)]
    public async Task<IActionResult> Edit(int id, SkillUpdate input, CancellationToken cancellationToken) =>
        Write(await management.SaveAsync(ActorId, FamilyId, id, new(Guid.Empty, input.Name, input.Category),
            input.Revision, input.Reason, HttpContext.TraceIdentifier, cancellationToken));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Archive(int id, SkillArchiveRequest input, CancellationToken cancellationToken)
    {
        var result = await management.SetArchiveAsync(ActorId, FamilyId, id, true, input.Revision,
            input.Reason, HttpContext.TraceIdentifier, cancellationToken);
        return result.Value is null ? Write(result) : NoContent();
    }

    [HttpPut("{id:int}/visibility")]
    [ProducesResponseType<ManagedSkill>(200)]
    public async Task<IActionResult> Visibility(int id, SkillVisibility input, CancellationToken cancellationToken) =>
        Write(await management.SetArchiveAsync(ActorId, FamilyId, id, input.Archived, input.Revision,
            input.Reason, HttpContext.TraceIdentifier, cancellationToken));

    private IActionResult Write(SkillWriteResult result, bool created = false)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (result.Forbidden) return Forbid();
        if (result.Missing) return NotFound();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        return created ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : Ok(result.Value);
    }
}
