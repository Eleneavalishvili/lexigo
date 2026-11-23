using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Classrooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Classrooms;

[ApiController]
[Authorize]
[Route("api/v1/classes")]
[RequestSizeLimit(8192)]
[EnableRateLimiting("learning")]
public sealed class ClassroomsController(ClassroomQueries queries, ClassroomManagement management,
    ClassInvitationService invitations, ClassJoiningService joining, ClassMemberService members) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [ProducesResponseType<ClassPage<ClassroomView>>(200)]
    public async Task<IActionResult> List([FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default)
    {
        Private();
        var value = await queries.ListAsync(UserId, FamilyId, page, cancellationToken);
        return value is null ? Forbid() : Ok(value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClassroomDetail>(200)]
    public async Task<IActionResult> Get(Guid id, [FromQuery, Range(1, 100000)] int memberPage = 1, CancellationToken cancellationToken = default)
    {
        Private();
        var value = await queries.DetailAsync(UserId, FamilyId, id, memberPage, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPost]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<ClassroomWriteResponse>(200)]
    [ProducesResponseType<ClassroomWriteResponse>(201)]
    public async Task<IActionResult> Create(CreateClassroomInput input, CancellationToken cancellationToken) =>
        Result(await management.CreateAsync(UserId, FamilyId, input, HttpContext.TraceIdentifier, cancellationToken));

    [HttpPatch("{id:guid}")]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<ClassroomWriteResponse>(200)]
    public async Task<IActionResult> Edit(Guid id, EditClassroomInput input, CancellationToken cancellationToken) =>
        Result(await management.EditAsync(UserId, FamilyId, id, input, HttpContext.TraceIdentifier, cancellationToken));

    [HttpPost("{id:guid}/invitations")]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<InvitationCreated>(200)]
    public async Task<IActionResult> Invite(Guid id, InvitationRequest input, CancellationToken cancellationToken)
    {
        Private();
        var value = await invitations.RotateAsync(UserId, FamilyId, id, input.Days, input.MaxUses, HttpContext.TraceIdentifier, cancellationToken);
        return value.Forbidden ? Forbid() : value.Missing ? NotFound() : value.Error is { } error
            ? Problem(statusCode: 409, title: error) : Ok(value.Value);
    }

    [HttpPost("join")]
    [EnableRateLimiting("account-entry")]
    [ProducesResponseType<ClassroomWriteResponse>(200)]
    public async Task<IActionResult> Join(JoinClassRequest input, CancellationToken cancellationToken) =>
        Result(await joining.JoinAsync(UserId, FamilyId, input.Code, HttpContext.TraceIdentifier, cancellationToken));

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<ClassroomWriteResponse>(200)]
    public async Task<IActionResult> Remove(Guid id, Guid userId, CancellationToken cancellationToken) =>
        Result(await members.ChangeAsync(UserId, FamilyId, id, userId, false, HttpContext.TraceIdentifier, cancellationToken));

    [HttpPatch("{id:guid}/members/{userId:guid}")]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<ClassroomWriteResponse>(200)]
    public async Task<IActionResult> Restore(Guid id, Guid userId, RestoreMemberRequest input, CancellationToken cancellationToken) =>
        input.Status != "Active" ? BadRequest() : Result(await members.ChangeAsync(UserId, FamilyId, id, userId, true, HttpContext.TraceIdentifier, cancellationToken));

    private void Private() => Response.Headers.CacheControl = "private, no-store";

    private IActionResult Result(ClassroomOutcome result)
    {
        Private();
        if (result.Forbidden) return Forbid();
        if (result.Missing) return NotFound();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        var response = new ClassroomWriteResponse(result.Id!.Value);
        return result.Created ? CreatedAtAction(nameof(Get), new { id = response.Id }, response) : Ok(response);
    }
}

public sealed record ClassroomWriteResponse(Guid Id);
public sealed record InvitationRequest([Range(1, 30)] int Days, [Range(1, 200)] int MaxUses);
public sealed record JoinClassRequest([Required, MaxLength(64)] string Code);
public sealed record RestoreMemberRequest([Required] string Status);
