using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[Route("api/v1")]
[RequestSizeLimit(16384)]
[EnableRateLimiting("learning")]
public sealed class LearningController(EnrollmentService enrollments, LearningSessionService sessions,
    LearningQueries queries, LearningAnswerService answers, LearningCompletionService completion, LearningAudioService audio,
    HeartBalanceService hearts, MistakeQueries mistakes, MistakePracticeService mistakePractice) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("me/hearts")]
    [ProducesResponseType<HeartStatus>(200)]
    public async Task<IActionResult> Hearts(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await hearts.StatusAsync(UserId, cancellationToken));
    }

    [HttpGet("me/enrollments")]
    [ProducesResponseType<EnrollmentPage>(200)]
    public async Task<IActionResult> Enrollments([FromQuery, Range(1, 100000)] int page = 1, CancellationToken cancellationToken = default) =>
        Ok(await enrollments.ListAsync(UserId, page, cancellationToken));

    [HttpPost("me/enrollments")]
    [ProducesResponseType<EnrollmentReceipt>(200)]
    [ProducesResponseType<EnrollmentReceipt>(201)]
    public async Task<IActionResult> Enroll(EnrollRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var outcome = await enrollments.EnrollAsync(UserId, FamilyId, request.CourseId, cancellationToken, request.StartingLevel);
        return outcome.Created && outcome.Value is EnrollmentReceipt receipt
            ? CreatedAtAction(nameof(Enrollment), new { courseId = receipt.CourseId }, receipt)
            : Result(outcome);
    }

    [HttpGet("me/enrollments/{courseId:guid}")]
    [ProducesResponseType<EnrollmentView>(200)]
    public async Task<IActionResult> Enrollment(Guid courseId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var enrollment = await enrollments.GetAsync(UserId, courseId, cancellationToken);
        return enrollment is null ? NotFound() : Ok(enrollment);
    }

    [HttpGet("me/progress")]
    [ProducesResponseType<CourseProgress>(200)]
    public async Task<IActionResult> Progress([FromQuery] Guid courseId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var value = await enrollments.ProgressAsync(UserId, courseId, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPost("lessons/{id:guid}/sessions")]
    [ProducesResponseType<SessionView>(200)]
    [ProducesResponseType<SessionView>(201)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        Result(await sessions.StartAsync(UserId, FamilyId, id, false, cancellationToken));

    [HttpPost("me/practice-sessions")]
    [ProducesResponseType<SessionView>(200)]
    [ProducesResponseType<SessionView>(201)]
    public async Task<IActionResult> Practice(PracticeRequest request, CancellationToken cancellationToken)
    {
        if (request.Focus is not null && (request.Focus is not ("repeated" or "due")
            || !request.MistakesOnly || request.SkillId.HasValue))
            return Problem(statusCode: 400, title: "Choose a course for focused practice without a skill filter.");
        if (request.MistakesOnly && request.CourseId is { } courseId && request.LessonId is null)
            return Result(await mistakePractice.StartAsync(UserId, FamilyId, courseId, cancellationToken, request.SkillId,
                request.Focus));
        if (!request.MistakesOnly && request.LessonId is { } lessonId && request.CourseId is null && request.SkillId is null)
            return Result(await sessions.StartAsync(UserId, FamilyId, lessonId, true, cancellationToken));
        return Problem(statusCode: 400, title: "Choose a lesson, or a course with mistakesOnly, but not both.");
    }

    [HttpGet("me/mistakes")]
    [ProducesResponseType<MistakePage>(200)]
    public async Task<IActionResult> Mistakes([FromQuery] Guid courseId, [FromQuery, Range(1, int.MaxValue)] int? skillId = null,
        [FromQuery, Range(1, 100000)] int page = 1, [FromQuery] bool includeDue = false,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await mistakes.ListAsync(UserId, courseId, page, cancellationToken, skillId, includeDue);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("me/learning-sessions/{id:guid}", Name = SessionDetailRoute)]
    [ProducesResponseType<SessionView>(200)]
    public async Task<IActionResult> Session(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (await mistakePractice.IsLockedAsync(UserId, id, cancellationToken)) return FocusedPracticeRequired();
        var value = await queries.SessionAsync(UserId, id, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    internal const string SessionDetailRoute = "LearningSessionDetail";

    [HttpPost("me/learning-sessions/{id:guid}/answers")]
    [ProducesResponseType<AnswerFeedback>(200)]
    public async Task<IActionResult> Answer(Guid id, AnswerRequest request, CancellationToken cancellationToken)
    {
        if (await mistakePractice.IsLockedAsync(UserId, id, cancellationToken)) return FocusedPracticeRequired();
        return Result(await answers.AnswerAsync(UserId, FamilyId, id,
            new(request.SessionExerciseId, request.AnswerText, request.SelectedOptionIds, request.IdempotencyKey,
                request.OrderedTokenIds, request.MatchedPairs, request.PracticeCompleted), cancellationToken));
    }

    [HttpPost("me/learning-sessions/{id:guid}/complete")]
    [ProducesResponseType<SessionView>(200)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        if (await mistakePractice.IsLockedAsync(UserId, id, cancellationToken)) return FocusedPracticeRequired();
        return Result(await completion.CompleteAsync(UserId, FamilyId, id, cancellationToken));
    }

    [HttpGet("me/learning-sessions/{id:guid}/exercises/{exerciseId:guid}/audio")]
    [ProducesResponseType<AudioPlaybackLink>(200)]
    public async Task<IActionResult> Audio(Guid id, Guid exerciseId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (await mistakePractice.IsLockedAsync(UserId, id, cancellationToken)) return FocusedPracticeRequired();
        return Result(await audio.PlaybackAsync(UserId, FamilyId, id, exerciseId, cancellationToken));
    }

    [HttpGet("me/learning-sessions/{id:guid}/exercises/{exerciseId:guid}/options/{optionId:guid}/audio")]
    [ProducesResponseType<AudioPlaybackLink>(200)]
    public async Task<IActionResult> OptionAudio(Guid id, Guid exerciseId, Guid optionId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (await mistakePractice.IsLockedAsync(UserId, id, cancellationToken)) return FocusedPracticeRequired();
        return Result(await audio.PlaybackAsync(UserId, FamilyId, id, exerciseId, cancellationToken, optionId));
    }

    private IActionResult FocusedPracticeRequired() => Problem(statusCode: 403, title: "Focused practice requires Premium.");

    private IActionResult Result(LearningOutcome outcome)
    {
        if (outcome.Missing) return NotFound();
        if (outcome.PremiumRequired) return FocusedPracticeRequired();
        if (outcome.Unavailable)
            return Problem(statusCode: 503, title: "Audio playback is temporarily unavailable. Try again later.");
        if (outcome.Error is { } error) return Problem(statusCode: 409, title: error);
        return outcome.Created && outcome.Value is SessionView session
            ? CreatedAtAction(nameof(Session), new { id = session.Id }, session)
            : Ok(outcome.Value);
    }
}

public sealed record EnrollRequest(Guid CourseId, [RegularExpression("^(A1|A2|B1|B2|C1|C2)$")] string? StartingLevel = null);
public sealed record PracticeRequest(Guid? LessonId = null, Guid? CourseId = null, bool MistakesOnly = false,
    [Range(1, int.MaxValue)] int? SkillId = null, [StringLength(20)] string? Focus = null);
public sealed record AnswerRequest(Guid SessionExerciseId, [StringLength(1000)] string? AnswerText,
    [Required, MaxLength(8)] Guid[] SelectedOptionIds, Guid IdempotencyKey, [MaxLength(20)] Guid[]? OrderedTokenIds = null, [MaxLength(10)] MatchedPair[]? MatchedPairs = null, bool PracticeCompleted = false);
