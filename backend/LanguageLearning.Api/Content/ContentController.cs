using System.ComponentModel.DataAnnotations;
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
public sealed class ContentController(ContentQueries queries, ContentStructureService structures, LessonDraftService drafts,
    LessonWorkflow workflow, PrerequisiteService prerequisites,
    LessonDraftErasure erasure, LessonOwnershipService ownership) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);
    private string TraceId => HttpContext.TraceIdentifier;

    [HttpPost("lessons/{id:guid}/owner")]
    public async Task<IActionResult> TransferOwner(Guid id, TransferLessonOwner request, CancellationToken token) =>
        Write(await ownership.TransferAsync(ActorId, FamilyId, id, request, TraceId, token));

    [HttpDelete("lessons/{id:guid}/versions/{versionId:guid}/private-text")]
    public async Task<IActionResult> ErasePrivateText(Guid id, Guid versionId, CancellationToken cancellationToken) =>
        Write(await erasure.EraseAsync(ActorId, FamilyId, id, versionId, TraceId, cancellationToken));

    [HttpGet("courses")]
    [EnableRateLimiting("content-read")]
    public async Task<IActionResult> Courses(CancellationToken cancellationToken) => Read(await queries.CoursesAsync(ActorId, FamilyId, cancellationToken));
    [HttpPut("courses/{id:guid}")]
    public async Task<IActionResult> EditCourse(Guid id, EditCourseRequest request, CancellationToken cancellationToken) =>
        Write(await structures.EditCourseAsync(ActorId, FamilyId, id, request.Title, request.Description, TraceId, cancellationToken));
    [HttpPut("courses/{id:guid}/position")]
    public async Task<IActionResult> MoveCourse(Guid id, MovePositionRequest request, CancellationToken cancellationToken) =>
        Write(await structures.MoveCourseAsync(ActorId, FamilyId, id, request.FromPosition, request.ToPosition, TraceId, cancellationToken));
    [HttpPut("courses/{id:guid}/archive")]
    public async Task<IActionResult> SetCourseArchive(Guid id, ArchiveVisibilityRequest request,
        CancellationToken cancellationToken) =>
        Write(await structures.SetCourseArchiveAsync(ActorId, FamilyId, id, request.Archived!.Value, request.Reason, TraceId, cancellationToken));
    [HttpGet("units")]
    [EnableRateLimiting("content-read")]
    public async Task<IActionResult> Units([FromQuery] Guid courseId, CancellationToken cancellationToken) => Read(await queries.UnitsAsync(ActorId, FamilyId, courseId, cancellationToken));
    [HttpGet("units/{id:guid}")]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<StaffUnit>(200)]
    public async Task<IActionResult> Unit(Guid id, CancellationToken cancellationToken) =>
        Read(await queries.UnitAsync(ActorId, FamilyId, id, cancellationToken));
    [HttpPut("units/{id:guid}/archive")]
    public async Task<IActionResult> SetUnitArchive(Guid id, ArchiveVisibilityRequest request,
        CancellationToken cancellationToken) =>
        Write(await structures.SetUnitArchiveAsync(ActorId, FamilyId, id, request.Archived!.Value, request.Reason, TraceId, cancellationToken));
    [HttpPost("units")]
    [ProducesResponseType<ContentCreatedResponse>(201)]
    public async Task<IActionResult> CreateUnit(UnitCreateRequest request, CancellationToken cancellationToken) =>
        Write(await structures.CreateUnitAsync(ActorId, FamilyId,
            request.CourseId, new(request.Title, request.Description, request.OrderNumber, request.Difficulty),
            TraceId, cancellationToken), detailAction: nameof(Unit));
    [HttpPut("units/{id:guid}")]
    public async Task<IActionResult> EditUnit(Guid id, UnitEditRequest request, CancellationToken cancellationToken) =>
        Write(await structures.EditUnitAsync(ActorId, FamilyId, id,
            new(request.Title, request.Description, request.OrderNumber, request.Difficulty), TraceId, cancellationToken));
    [HttpPut("units/{id:guid}/position")]
    public async Task<IActionResult> MoveUnit(Guid id, MovePositionRequest request, CancellationToken cancellationToken) =>
        Write(await structures.MoveUnitAsync(ActorId, FamilyId, id, request.FromPosition, request.ToPosition, TraceId, cancellationToken));
    [HttpGet("lessons")]
    [EnableRateLimiting("content-read")]
    public async Task<IActionResult> Lessons([FromQuery] Guid courseId, [FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default) =>
        Read(await queries.LessonsAsync(ActorId, FamilyId, courseId, page, cancellationToken));
    [HttpGet("lessons/{id:guid}")]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<StaffLesson>(200)]
    public async Task<IActionResult> Lesson(Guid id, [FromQuery] Guid? versionId, CancellationToken cancellationToken) => Read(await queries.LessonAsync(ActorId, FamilyId, id, versionId, cancellationToken));
    [HttpPost("lessons/{id:guid}/preview/check")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<PreviewFeedback>(200)]
    public async Task<IActionResult> PreviewCheck(Guid id, PreviewCheckRequest request,
        [FromServices] LessonPreviewGrading grading,
        CancellationToken cancellationToken) =>
        Read(await grading.CheckAsync(ActorId, FamilyId,
            id, request.VersionId, request.ExerciseId, request.Answer, request.OrderedTokenIds ?? [],
            request.MatchedPairs ?? [], cancellationToken));
    [HttpPost("lessons")]
    [ProducesResponseType<ContentCreatedResponse>(201)]
    public async Task<IActionResult> Create(LessonCreateRequest request, CancellationToken cancellationToken) =>
        Write(await drafts.CreateAsync(ActorId, FamilyId,
            new(request.UnitId, request.Title, request.Objective, request.Explanation, request.OrderNumber),
            TraceId, cancellationToken), detailAction: nameof(Lesson));
    [HttpPut("lessons/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, DraftUpdateRequest request, CancellationToken cancellationToken) => Write(await drafts.EditAsync(ActorId, FamilyId, id,
        new(request.VersionId, request.Title, request.Objective, request.Explanation), TraceId, cancellationToken));
    [HttpPut("lessons/{id:guid}/position")]
    public async Task<IActionResult> MoveLesson(Guid id, MovePositionRequest request, CancellationToken cancellationToken) =>
        Write(await drafts.MoveAsync(ActorId, FamilyId, id, request.FromPosition, request.ToPosition, TraceId, cancellationToken));
    [HttpPost("lessons/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, VersionRequest request, CancellationToken cancellationToken) => Write(await workflow.TransitionAsync(ActorId, FamilyId, id, request.VersionId, "submit", "Submitted for independent review.", TraceId, cancellationToken));
    [HttpPost("lessons/{id:guid}/reviews")]
    public async Task<IActionResult> Review(Guid id, ReviewRequest request, CancellationToken cancellationToken) => Write(await workflow.TransitionAsync(ActorId, FamilyId, id, request.VersionId, request.Decision, request.Comment, TraceId, cancellationToken));
    [HttpPost("lessons/{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, VersionRequest request, CancellationToken cancellationToken) => Write(await workflow.TransitionAsync(ActorId, FamilyId, id, request.VersionId, "publish", "Independently approved version published.", TraceId, cancellationToken));
    [HttpDelete("lessons/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, ArchiveRequest request, CancellationToken cancellationToken) => Write(await workflow.TransitionAsync(ActorId, FamilyId, id, request.VersionId, "archive", request.Reason, TraceId, cancellationToken));
    [HttpGet("lessons/{id:guid}/versions")]
    [EnableRateLimiting("content-read")]
    public async Task<IActionResult> Versions(Guid id, [FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default) => Read(await queries.VersionsAsync(ActorId, FamilyId, id, page, cancellationToken));
    [HttpPost("lessons/{id:guid}/versions")]
    [ProducesResponseType<ContentCreatedResponse>(201)]
    public async Task<IActionResult> Revision(Guid id, CancellationToken cancellationToken)
    {
        var outcome = await workflow.RevisionAsync(ActorId, FamilyId, id, TraceId, cancellationToken);
        if (outcome.Forbidden || outcome.Error is not null) return Write(outcome);
        return CreatedAtAction(nameof(Lesson), new { id, versionId = outcome.Id },
            new ContentCreatedResponse(outcome.Id!.Value));
    }
    [HttpPost("lessons/{id:guid}/duplicate")]
    [ProducesResponseType<ContentCreatedResponse>(201)]
    public async Task<IActionResult> Duplicate(Guid id, DuplicateLessonRequest request, CancellationToken cancellationToken) =>
        Write(await workflow.DuplicateAsync(ActorId, FamilyId, id, request.VersionId, request.Title,
            TraceId, cancellationToken), detailAction: nameof(Lesson));
    [HttpPut("lessons/{id:guid}/prerequisites")]
    public async Task<IActionResult> Prerequisites(Guid id, PrerequisiteRequest request, CancellationToken cancellationToken) => Write(await prerequisites.SaveAsync(ActorId, FamilyId, id, request.LessonIds, TraceId, cancellationToken));

    private IActionResult Read(object? value) => value is null ? NotFound() : Ok(value);
    private IActionResult Write(ContentOutcome outcome, string? detailAction = null)
    {
        if (outcome.Forbidden) return Forbid();
        if (outcome.Error is { } error) return Problem(statusCode: 409, title: error);
        return detailAction is not null
            ? CreatedAtAction(detailAction, new { id = outcome.Id }, new ContentCreatedResponse(outcome.Id!.Value))
            : Ok(new { id = outcome.Id });
    }

}
