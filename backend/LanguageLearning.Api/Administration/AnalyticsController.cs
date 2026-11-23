using System.Globalization;
using System.Text;
using LanguageLearning.Infrastructure.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Administration;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/v1/admin/analytics")]
[EnableRateLimiting("content-write")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AnalyticsController(LearningAnalytics analytics) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<LearningAnalyticsView>(200)]
    public async Task<IActionResult> Read([FromQuery] DateOnly from, [FromQuery] DateOnly through,
        [FromQuery] Guid? courseId, [FromQuery] bool csv, CancellationToken token)
    {
        if (from == default || through == DateOnly.MaxValue || through < from || through.DayNumber - from.DayNumber >= 90)
            return BadRequest("Choose a date range of 1 to 90 days.");

        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(through.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var report = await analytics.ReadAsync(Guid.Parse(User.FindFirst("sub")!.Value),
            Guid.Parse(User.FindFirst("sid")!.Value), courseId, start, end, token);
        if (report is null) return Forbid();
        if (!csv) return Ok(report);
        return File(Encoding.UTF8.GetBytes(Export(report)), "text/csv; charset=utf-8", "lexigo-analytics.csv");
    }

    private static string Export(LearningAnalyticsView report)
    {
        var output = new StringBuilder("section,id,label,learners,sessions_started,lessons_finished,answers,errors\r\n");
        Row("total", "", "Active learners", report.ActiveLearners);
        foreach (var course in report.Courses)
            Row("course", course.CourseId.ToString(), course.Course, course.Learners, course.Started, course.Finished);
        foreach (var format in report.Difficulty)
            Row("format", "", format.Format, answers: format.Answers, errors: format.Errors);
        foreach (var exercise in report.CommonErrors)
            Row("exercise", exercise.ExerciseId.ToString(), exercise.Format, answers: exercise.Answers, errors: exercise.Errors);
        return output.ToString();

        void Row(string section, string id, string label, int? learners = null, int? started = null,
            int? finished = null, int? answers = null, int? errors = null)
        {
            if (label.TrimStart() is { Length: > 0 } trimmed && trimmed[0] is '=' or '+' or '-' or '@') label = "'" + label;
            var numbers = new[] { learners, started, finished, answers, errors };
            output.Append(section).Append(',').Append(id).Append(",\"")
                .Append(label.Replace("\"", "\"\"")).Append("\",")
                .AppendJoin(',', numbers.Select(number => number?.ToString(CultureInfo.InvariantCulture)))
                .Append("\r\n");
        }
    }
}
