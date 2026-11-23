using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Content;

[ApiController]
[Authorize]
[Route("api/v1")]
[RequestSizeLimit(8192)]
[EnableRateLimiting("learning")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportsController(ContentReportService reports) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpPost("reports")]
    [ProducesResponseType<ContentReportView>(200)]
    [ProducesResponseType<ContentReportView>(201)]
    public async Task<IActionResult> Create(CreateContentReport input, CancellationToken token)
    {
        var result = await reports.CreateAsync(UserId, FamilyId, input, token);
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        if (result.Report is not { } report) return NotFound();
        return result.Created ? CreatedAtAction(nameof(MineDetail), new { id = report.Id }, report) : Ok(report);
    }

    [HttpGet("me/reports")]
    [ProducesResponseType<ContentReportView[]>(200)]
    public async Task<IActionResult> Mine([FromQuery, Range(1, 10000)] int page = 1,
        CancellationToken token = default) => Ok(await reports.MineAsync(UserId, page, token));

    [HttpPost("reports/public-names")]
    [ProducesResponseType<ContentReportView>(200)]
    [ProducesResponseType<ContentReportView>(201)]
    public async Task<IActionResult> ReportPublicName(CreatePublicNameReport input, CancellationToken token)
    {
        var result = await reports.ReportPublicNameAsync(UserId, FamilyId, input, token);
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        if (result.Report is not { } report) return NotFound();
        return result.Created ? CreatedAtAction(nameof(MineDetail), new { id = report.Id }, report) : Ok(report);
    }

    [HttpGet("me/reports/{id:guid}")]
    [ProducesResponseType<ContentReportView>(200)]
    public async Task<IActionResult> MineDetail(Guid id, CancellationToken token) =>
        await reports.MineDetailAsync(UserId, id, token) is { } result ? Ok(result) : NotFound();

    [HttpPost("reports/ranking-names")]
    [ProducesResponseType<ContentReportView>(200)]
    [ProducesResponseType<ContentReportView>(201)]
    public async Task<IActionResult> ReportRankingName(CreateRankingNameReport input, CancellationToken token)
    {
        var result = await reports.ReportRankingNameAsync(UserId, FamilyId, input, token);
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        if (result.Report is not { } report) return NotFound();
        return result.Created ? CreatedAtAction(nameof(MineDetail), new { id = report.Id }, report) : Ok(report);
    }

    [HttpGet("admin/reports")]
    [ProducesResponseType<ContentReportView[]>(200)]
    public async Task<IActionResult> StaffList([FromQuery, Range(1, 10000)] int page = 1,
        CancellationToken token = default) =>
        await reports.StaffReadAsync(UserId, FamilyId, null, page, token) is { } result ? Ok(result) : NotFound();

    [HttpGet("admin/reports/{id:guid}")]
    [ProducesResponseType<ContentReportView>(200)]
    public async Task<IActionResult> StaffDetail(Guid id, CancellationToken token) =>
        await reports.StaffReadAsync(UserId, FamilyId, id, 1, token) is { } result ? Ok(result) : NotFound();

    [HttpPatch("admin/reports/{id:guid}")]
    [EnableRateLimiting("content-write")]
    [ProducesResponseType<ContentReportView>(200)]
    public async Task<IActionResult> Review(Guid id, ReviewContentReport input, CancellationToken token) =>
        ReportResult(await reports.ReviewAsync(UserId, FamilyId, id, input, HttpContext.TraceIdentifier, token));

    private IActionResult ReportResult(LearningOutcome result)
    {
        if (result.Missing) return NotFound();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        return Ok(result.Value);
    }
}
