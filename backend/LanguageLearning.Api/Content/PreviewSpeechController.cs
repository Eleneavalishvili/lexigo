using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[Authorize(Roles = "ContentAuthor,Reviewer,Admin,SuperAdmin")]
[Route("api/v1/manage/lessons/{lessonId:guid}/versions/{versionId:guid}/exercises/{exerciseId:guid}/preview-speech")]
[EnableRateLimiting("speech-check")]
public sealed class PreviewSpeechController(LessonPreviewGrading grading) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1048576)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1048576)]
    [ProducesResponseType<PreviewSpeechFeedback>(200)]
    public async Task<IActionResult> Check(Guid lessonId, Guid versionId, Guid exerciseId,
        IFormFile recording, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var form = await Request.ReadFormAsync(cancellationToken);
        if (form.Count != 0 || form.Files.Count != 1 || recording.Length is < 44 or > 960044)
            return Problem(statusCode: 400, title: "Send one short WAV recording.");

        PreparedAudio audio;
        try
        {
            await using var input = recording.OpenReadStream();
            audio = await WaveAudioInspector.ReadAsync(input, cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Problem(statusCode: 400, title: "The recording is not a valid short PCM WAV file.");
        }
        if (audio.Channels != 1 || audio.BitsPerSample != 16 || audio.SampleRate != 16000 || audio.DurationMilliseconds > 30000)
            return Problem(statusCode: 400, title: "Use mono 16-bit WAV at 16000 Hz, up to 30 seconds.");

        var result = await grading.CheckSpeechAsync(Guid.Parse(User.FindFirst("sub")!.Value),
            Guid.Parse(User.FindFirst("sid")!.Value), lessonId, versionId, exerciseId, audio, cancellationToken);
        if (result is null) return NotFound();
        return result.Status == "Unavailable" ? Problem(statusCode: 503, title: result.Message) : Ok(result);
    }
}
