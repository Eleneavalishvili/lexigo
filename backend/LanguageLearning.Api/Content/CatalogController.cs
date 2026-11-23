using LanguageLearning.Infrastructure.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("content-read")]
public sealed class CatalogController(CatalogQueries catalog) : ControllerBase
{
    [HttpGet("api/v1/courses")]
    [ProducesResponseType<CatalogCourse[]>(200)]
    public async Task<IActionResult> Courses(CancellationToken cancellationToken) => Ok(await catalog.CoursesAsync(cancellationToken));
    [HttpGet("api/v1/courses/{id:guid}")]
    [ProducesResponseType<CatalogCourse>(200)]
    public async Task<IActionResult> Course(Guid id, CancellationToken cancellationToken) => Read(await catalog.CourseAsync(id, cancellationToken));
    [HttpGet("api/v1/courses/{id:guid}/units")]
    [ProducesResponseType<CatalogUnit[]>(200)]
    public async Task<IActionResult> Units(Guid id, CancellationToken cancellationToken) => Read(await catalog.UnitsAsync(id, cancellationToken));
    [HttpGet("api/v1/units/{id:guid}/lessons")]
    [ProducesResponseType<CatalogLesson[]>(200)]
    public async Task<IActionResult> Lessons(Guid id, CancellationToken cancellationToken) => Read(await catalog.LessonsAsync(id, cancellationToken));
    [HttpGet("api/v1/lessons/{id:guid}")]
    [ProducesResponseType<CatalogLesson>(200)]
    public async Task<IActionResult> Lesson(Guid id, CancellationToken cancellationToken) => Read(await catalog.LessonAsync(id, cancellationToken));
    private IActionResult Read(object? value) => value is null ? NotFound() : Ok(value);
}
