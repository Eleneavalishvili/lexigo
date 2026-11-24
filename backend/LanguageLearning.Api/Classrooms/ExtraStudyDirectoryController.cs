using LanguageLearning.Infrastructure.Classrooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Classrooms;

[ApiController]
[Authorize]
[Route("api/v1/extra-study")]
[EnableRateLimiting("learning")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExtraStudyDirectoryController(
    ExtraStudyDirectory directory, ExtraStudyRequests requests, ExtraStudySpace spaces) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("requests/{requestId:guid}/space")]
    [ProducesResponseType<ExtraStudySpaceView>(200)]
    public async Task<IActionResult> StudySpace(Guid requestId, CancellationToken cancellationToken)
    {
        var result = await spaces.ReadAsync(UserId, FamilyId, requestId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("requests/{requestId:guid}/space/lessons/{lessonId:guid}")]
    public async Task<IActionResult> ShareLesson(Guid requestId, Guid lessonId, CancellationToken cancellationToken) =>
        WriteResult(await spaces.ShareAsync(UserId, FamilyId, requestId, lessonId, cancellationToken));

    [HttpPut("requests/{requestId:guid}/space/lessons/{lessonId:guid}/schedule")]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> ScheduleLesson(Guid requestId, Guid lessonId,
        ExtraStudyScheduleInput input, CancellationToken cancellationToken) =>
        WriteResult(await spaces.ScheduleAsync(UserId, FamilyId, requestId, lessonId, input, cancellationToken));

    [HttpGet("requests")]
    [ProducesResponseType<ExtraStudyRequestView[]>(200)]
    public async Task<IActionResult> ListRequests(Guid? after, CancellationToken cancellationToken)
    {
        var result = await requests.ListAsync(UserId, FamilyId, after, cancellationToken);
        return result is null ? Forbid() : Ok(result);
    }

    [HttpPost("offers/{offerId:guid}/requests")]
    public async Task<IActionResult> CreateRequest(Guid offerId, CancellationToken cancellationToken) =>
        WriteResult(await requests.RequestAsync(UserId, FamilyId, offerId, cancellationToken));

    [HttpPut("requests/{requestId:guid}/decision")]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> Decide(Guid requestId, ExtraStudyDecisionRequest request, CancellationToken cancellationToken) =>
        WriteResult(await requests.DecideAsync(UserId, FamilyId, requestId, request.Accept, cancellationToken));

    [HttpDelete("requests/{requestId:guid}")]
    public async Task<IActionResult> CloseRequest(Guid requestId, CancellationToken cancellationToken) =>
        WriteResult(await requests.CloseAsync(UserId, FamilyId, requestId, cancellationToken));

    private IActionResult WriteResult(ClassroomOutcome result) =>
        result.Forbidden ? Forbid() : result.Missing ? NotFound() : result.Error is { } error
            ? Problem(statusCode: 409, title: error) : Ok(new { id = result.Id });

    [HttpGet("teachers")]
    [ProducesResponseType<ExtraStudyTeacher[]>(200)]
    public async Task<IActionResult> AllTeachers(Guid? after, CancellationToken token)
    {
        var teachers = await directory.FindAsync(UserId, FamilyId, Guid.Empty, after, token);
        return teachers is null ? Forbid() : Ok(teachers);
    }

    [HttpGet("courses/{courseId:guid}/teachers")]
    [ProducesResponseType<ExtraStudyTeacher[]>(200)]
    public async Task<IActionResult> Teachers(Guid courseId, Guid? after, CancellationToken cancellationToken)
    {
        var teachers = await directory.FindAsync(UserId, FamilyId, courseId, after, cancellationToken);
        return teachers is null ? Forbid() : Ok(teachers);
    }

    [HttpGet("availability")]
    [ProducesResponseType<ExtraStudyAvailability[]>(200)]
    public async Task<IActionResult> Availability(CancellationToken cancellationToken)
    {
        var offers = await directory.OwnAsync(UserId, FamilyId, cancellationToken);
        return offers is null ? Forbid() : Ok(offers);
    }

    [HttpPut("availability/{courseId:guid}")]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> Update(Guid courseId, ExtraStudyAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await directory.SetAsync(UserId, FamilyId, courseId, request.Enabled, cancellationToken);
        return result switch
        {
            ExtraStudyWriteResult.Saved => NoContent(),
            ExtraStudyWriteResult.CourseUnavailable => NotFound(),
            _ => Forbid(),
        };
    }
}

public sealed record ExtraStudyAvailabilityRequest(bool Enabled);
public sealed class ExtraStudyDecisionRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public bool? Accepted { get; init; }

    public bool Accept => Accepted == true;
}
