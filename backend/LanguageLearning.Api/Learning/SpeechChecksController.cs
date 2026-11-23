using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1/me/learning-sessions/{id:guid}/speech-checks")]
[EnableRateLimiting("speech-check")]
public sealed class SpeechChecksController(SpeechCheckService checks) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1048576)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1048576)]
    [ProducesResponseType<SpeechCheckFeedback>(200)]
    public async Task<IActionResult> Check(Guid id, [FromForm] SpeechCheckRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var form = await Request.ReadFormAsync(cancellationToken);
        if (form.Keys.Any(key => !new[] { "ExerciseId", "IdempotencyKey" }.Contains(key, StringComparer.OrdinalIgnoreCase))
            || form.Any(field => field.Value.Count != 1)
            || form.Files.Count != 1 || !string.Equals(form.Files[0].Name, "Recording", StringComparison.OrdinalIgnoreCase)
            || request.Recording.Length is < 44 or > 960044)
            return Problem(statusCode: 400, title: "Send one short recording, its exercise and request key only.");
        PreparedAudio audio;
        try
        {
            await using var input = request.Recording.OpenReadStream();
            audio = await WaveAudioInspector.ReadAsync(input, cancellationToken);
        }
        catch (InvalidDataException) { return Problem(statusCode: 400, title: "The recording is not a valid short PCM WAV file."); }
        var result = await checks.CheckAsync(Guid.Parse(User.FindFirst("sub")!.Value), Guid.Parse(User.FindFirst("sid")!.Value),
            id, request.ExerciseId, request.IdempotencyKey, audio, cancellationToken);
        if (result.RateLimited)
        {
            Response.Headers.RetryAfter = "600";
            return Problem(statusCode: 429, title: "Take a short break before checking more recordings.");
        }
        return result.Missing ? NotFound() : result.Unavailable
            ? Problem(statusCode: 503, title: "Local voice checking is unavailable or busy. No heart was used; try again shortly.")
            : result.Error is { } error ? Problem(statusCode: 409, title: error) : Ok(result.Value);
    }
}

public sealed class SpeechCheckRequest
{
    public Guid ExerciseId { get; init; }
    public Guid IdempotencyKey { get; init; }
    [Required] public required IFormFile Recording { get; init; }
}
