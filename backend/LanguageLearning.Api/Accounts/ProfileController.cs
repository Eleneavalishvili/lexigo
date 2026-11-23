using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public sealed class ProfileController(ProfileService profiles) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPatch]
    [EnableRateLimiting("account-entry")]
    [ProducesResponseType<RenamedProfile>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Rename(RenameProfileRequest request, CancellationToken cancellationToken)
    {
        string? name = await profiles.RenameAsync(UserId, request.DisplayName, cancellationToken);
        return name is null
            ? Problem(statusCode: 400, title: "Enter a display name of 2–80 characters without control characters.")
            : Ok(new RenamedProfile(name));
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> Preferences(CancellationToken cancellationToken)
    {
        var value = await profiles.GetAsync(UserId, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPut("preferences")]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> Save(PreferenceDetails request, CancellationToken cancellationToken) =>
        await profiles.SaveAsync(UserId, request, cancellationToken)
            ? NoContent() : Problem(statusCode: 400, title: "Check your time zone, goal, study days and course selection.");

    [HttpGet("preferences/courses")]
    public async Task<IActionResult> Courses(CancellationToken cancellationToken) => Ok(await profiles.CoursesAsync(cancellationToken));

    [HttpPut("study-goal")]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> StudyGoal(StudyGoalRequest request, CancellationToken cancellationToken) =>
        await profiles.SetStudyGoalAsync(UserId, request.DailyGoalXp, cancellationToken)
            ? NoContent() : Problem(statusCode: 400, title: "Choose a daily goal between 1 and 1000 XP.");
}

public sealed record RenameProfileRequest([Required, StringLength(80, MinimumLength = 2)] string DisplayName);
public sealed record RenamedProfile(string DisplayName);
public sealed record StudyGoalRequest([Range(1, 1000)] int DailyGoalXp);
