using LanguageLearning.Infrastructure.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[Authorize(Roles = "ContentAuthor,Reviewer,Admin,SuperAdmin")]
[Route("api/v1/manage/courses")]
[RequestSizeLimit(8192)]
public sealed class ManagedCoursesController(CourseCreation courses) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("languages")]
    [EnableRateLimiting("content-read")]
    public async Task<IActionResult> Languages(CancellationToken token)
    {
        var languages = await courses.LanguagesAsync(ActorId, FamilyId, token);
        return languages is null ? Forbid() : Ok(languages);
    }

    [HttpGet("{id:guid}")]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<StaffCourseDetail>(200)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken token)
    {
        var course = await courses.DetailAsync(ActorId, FamilyId, id, token);
        return course is null ? NotFound() : Ok(course);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<ContentCreatedResponse>(201)]
    public async Task<IActionResult> Create(CreateCourseRequest request, CancellationToken token)
    {
        var result = await courses.CreateAsync(ActorId, FamilyId, request, HttpContext.TraceIdentifier, token);
        if (result.Forbidden) return Forbid();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        return CreatedAtAction(nameof(Detail), new { id = result.Id }, new ContentCreatedResponse(result.Id!.Value));
    }
}
