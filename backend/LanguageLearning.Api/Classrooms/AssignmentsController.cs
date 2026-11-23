using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Classrooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Classrooms;

[ApiController]
[Authorize]
[Route("api/v1")]
[RequestSizeLimit(8192)]
[EnableRateLimiting("learning")]
public sealed class AssignmentsController(AssignmentQueries queries, AssignmentManagement management,
    AssignmentSubmissions submissions, AssignmentResults results, AssignmentFeedback feedback) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("classes/{id:guid}/assignments")]
    [ProducesResponseType<ClassPage<AssignmentSummary>>(200)]
    public async Task<IActionResult> List(Guid id, [FromQuery, Range(1, 100000)] int page = 1,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await queries.ListAsync(UserId, FamilyId, id, page, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("assignments/{id:guid}")]
    [ProducesResponseType<AssignmentDetail>(200)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await queries.DetailAsync(UserId, FamilyId, id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("classes/{id:guid}/assignments")]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<ClassroomWriteResponse>(200)]
    [ProducesResponseType<ClassroomWriteResponse>(201)]
    public async Task<IActionResult> Create(Guid id, CreateAssignmentInput input, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return WriteResult(await management.CreateAsync(UserId, FamilyId, id, input, HttpContext.TraceIdentifier, cancellationToken));
    }

    [HttpPatch("assignments/{id:guid}")]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<ClassroomWriteResponse>(200)]
    public async Task<IActionResult> Edit(Guid id, EditAssignmentInput input, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return WriteResult(await management.EditAsync(UserId, FamilyId, id, input, HttpContext.TraceIdentifier, cancellationToken));
    }

    [HttpPost("assignments/{id:guid}/submissions")]
    [ProducesResponseType<AssignmentReceipt>(200)]
    public async Task<IActionResult> Submit(Guid id, SubmitAssignmentInput input, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await submissions.SubmitAsync(UserId, FamilyId, id, input.SessionId, cancellationToken);
        if (result.Forbidden) return Forbid();
        if (result.Missing) return NotFound();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        return Ok(result.Receipt);
    }

    [HttpGet("assignments/{id:guid}/results")]
    [ProducesResponseType<AssignmentResultsPage>(200)]
    public async Task<IActionResult> Results(Guid id, [FromQuery, Range(1, 100000)] int page = 1,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await results.GetAsync(UserId, FamilyId, id, page, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("assignments/{id:guid}/feedback")]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<ClassroomWriteResponse>(200)]
    public async Task<IActionResult> Feedback(Guid id, WriteAssignmentFeedback input, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return WriteResult(await feedback.WriteAsync(UserId, FamilyId, id, input, HttpContext.TraceIdentifier, cancellationToken));
    }

    private IActionResult WriteResult(ClassroomOutcome result)
    {
        if (result.Forbidden) return Forbid();
        if (result.Missing) return NotFound();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        var response = new ClassroomWriteResponse(result.Id!.Value);
        return result.Created
            ? CreatedAtAction(nameof(Detail), new { id = response.Id }, response)
            : Ok(response);
    }
}
