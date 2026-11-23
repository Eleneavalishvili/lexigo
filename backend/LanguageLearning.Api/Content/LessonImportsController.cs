using LanguageLearning.Infrastructure.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[Authorize(Roles = "ContentAuthor,Admin,SuperAdmin")]
[Route("api/v1/manage/imports")]
[RequestSizeLimit(524288)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting("content-write")]
public sealed class LessonImportsController(LessonImports imports) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid Family => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpPost("validate")]
    [ProducesResponseType<LessonImportReview>(201)]
    public async Task<IActionResult> Validate(ValidateLessonImport input, CancellationToken token) =>
        Result(await imports.ValidateAsync(Actor, Family, input, token), createdReview: true);

    [HttpGet("{id:guid}")]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<LessonImportJobStatus>(200)]
    public async Task<IActionResult> Get(Guid id, CancellationToken token)
    {
        var value = await imports.GetAsync(Actor, Family, id, token);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPost("{id:guid}/commit")]
    [ProducesResponseType<ContentCreatedResponse>(201)]
    [ProducesResponseType<ContentCreatedResponse>(200)]
    public async Task<IActionResult> Commit(Guid id, CancellationToken token) =>
        Result(await imports.CommitAsync(Actor, Family, id, HttpContext.TraceIdentifier, token));

    private IActionResult Result(LessonImportResult result, bool createdReview = false)
    {
        if (result.Forbidden) return Forbid();
        if (result.Missing) return NotFound();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        if (result.Review is { } review) return createdReview
            ? CreatedAtAction(nameof(Get), new { id = review.Id }, review) : Ok(review);
        var response = new ContentCreatedResponse(result.LessonId!.Value);
        return result.Created
            ? CreatedAtAction(nameof(ContentController.Lesson), "Content", new { id = result.LessonId }, response)
            : Ok(response);
    }
}
