using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Vocabulary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Vocabulary;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/v1/manage/vocabulary")]
[EnableRateLimiting("content-write")]
[RequestSizeLimit(1024)]
public sealed class StaffVocabularyAudioController(StaffVocabularyAudio vocabulary) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [EnableRateLimiting("content-read")]
    [ProducesResponseType<StaffWordPage>(200)]
    public async Task<IActionResult> List([FromQuery, Required] Guid? courseId,
        [FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await vocabulary.ListAsync(ActorId, FamilyId, courseId!.Value, page, cancellationToken);
        return result is null ? Forbid() : Ok(result);
    }

    [HttpPut("{id:guid}/audio")]
    public async Task<IActionResult> Attach(Guid id, VocabularyAudioRequest request, CancellationToken cancellationToken)
    {
        var result = await vocabulary.AttachAsync(ActorId, FamilyId, id, request.AudioMediaId,
            request.PreviousAudioMediaId, HttpContext.TraceIdentifier, cancellationToken);
        if (result.Forbidden) return Forbid();
        return result.Error is { } error ? Problem(statusCode: 409, title: error) : NoContent();
    }
}

public sealed record VocabularyAudioRequest(Guid? AudioMediaId, Guid? PreviousAudioMediaId);
