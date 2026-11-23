using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/v1/manage/achievements")]
[RequestSizeLimit(8192)]
[EnableRateLimiting("content-write")]
public sealed class ManagedAchievementsController(RewardCatalogQueries queries, RewardCatalogAccess access,
    AchievementManagement management) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [ProducesResponseType<CatalogPage<ManagedAchievement>>(200)]
    [EnableRateLimiting("content-read")]
    public async Task<IActionResult> List([FromQuery, Range(1, 100000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, [FromQuery, MaxLength(100)] string? search = null,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!await access.AllowedAsync(ActorId, FamilyId, false, cancellationToken)) return Forbid();
        return Ok(await queries.AchievementsAsync(page, pageSize, search, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ManagedAchievement>(200)]
    [EnableRateLimiting("content-read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!await access.AllowedAsync(ActorId, FamilyId, false, cancellationToken)) return Forbid();
        var value = await queries.AchievementAsync(id, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPost]
    [ProducesResponseType<CatalogWriteResponse>(201)]
    public async Task<IActionResult> Create(AchievementInput input, CancellationToken cancellationToken) =>
        Result(await management.SaveAsync(ActorId, FamilyId, null, 0, input, HttpContext.TraceIdentifier, cancellationToken), created: true);

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CatalogWriteResponse>(200)]
    public async Task<IActionResult> Edit(Guid id, AchievementUpdateRequest request, CancellationToken cancellationToken) =>
        Result(await management.SaveAsync(ActorId, FamilyId, id, request.Revision, request.Input, HttpContext.TraceIdentifier, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType<CatalogWriteResponse>(200)]
    public async Task<IActionResult> Archive(Guid id, CatalogArchiveRequest request, CancellationToken cancellationToken) =>
        Result(await management.ArchiveAsync(ActorId, FamilyId, id, request.Revision, request.Reason, HttpContext.TraceIdentifier, cancellationToken));

    private IActionResult Result(CatalogWriteResult result, bool created = false)
    {
        Response.Headers.CacheControl = "private, no-store";
        return result.Forbidden ? Forbid() : result.Missing ? NotFound() : result.Error is { } error
            ? Problem(statusCode: 409, title: error) : created
                ? CreatedAtAction(nameof(Get), new { id = result.Id }, new CatalogWriteResponse(result.Id!.Value))
                : Ok(new CatalogWriteResponse(result.Id!.Value));
    }
}

public sealed record AchievementUpdateRequest([Range(1, int.MaxValue)] int Revision, [Required] AchievementInput Input);
public sealed record CatalogArchiveRequest([Range(1, int.MaxValue)] int Revision, [Required, MinLength(10), MaxLength(500)] string Reason);

public sealed record CatalogWriteResponse(Guid Id);
