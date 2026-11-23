using System.ComponentModel.DataAnnotations;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Administration;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed record CreateRankingNameReport(Guid CourseId, DateOnly Week,
    [Required, MaxLength(80)] string DisplayName, [Required, MaxLength(800)] string Comment,
    [Required, MaxLength(2048)] string? ReportToken = null);

public sealed partial class ContentReportService
{
    public async Task<ContentReportCreation> ReportRankingNameAsync(Guid userId, Guid familyId,
        CreateRankingNameReport input, CancellationToken token)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (!ValidText(input.Comment) || input.Comment.Length > 800 || input.Week.DayOfWeek != DayOfWeek.Monday
            || input.Week > today)
            return new(Error: "Choose a visible ranking entry and describe the problem.");

        await using var transaction = await database.Database.BeginTransactionAsync(token);
        await AdministrationLocks.AcquireAsync(database, [userId], token);
        if (!await learning.LockAsync(userId, familyId, token)) return new();
        var subject = reportTokens.Read(input.ReportToken);
        if (subject is null || subject.CourseId != input.CourseId || subject.Week != input.Week)
            return new(Error: "Refresh the ranking and open a new name report.");
        if (!await database.Enrollments.AnyAsync(item => item.UserId == userId && item.CourseId == input.CourseId, token))
            return new();
        var target = await LeaderboardScores.For(database, input.CourseId, input.Week)
            .SingleOrDefaultAsync(item => item.UserId == subject.UserId, token);
        if (target is null || target.UserId == userId) return new();
        if (target.DisplayName != input.DisplayName || target.DisplayName != subject.DisplayName)
            return new(Error: "This ranking changed. Refresh the ranking before reporting the name.");
        int languageId = await database.Courses.Where(item => item.Id == input.CourseId)
            .Select(item => item.TargetLanguageId).SingleAsync(token);
        string comment = $"Reported display name: {input.DisplayName}\n{input.Comment.Trim()}";
        var previous = await database.ContentReports.Where(item => item.ReporterId == userId
            && item.ResourceId == target.UserId && item.ResourceType == "RankingName"
            && (item.Status == "New" || item.Status == "InReview"))
            .OrderByDescending(item => item.CreatedAt).Select(View).FirstOrDefaultAsync(token);
        if (previous is not null)
            return previous.Comment == comment ? new(previous) : new(Error: "A report for this name is already open.");

        var report = new ContentReport
        {
            ReporterId = userId, LanguageId = languageId, ResourceType = "RankingName",
            ResourceId = target.UserId, Reason = "PublicName", Comment = comment, CreatedAt = clock.GetUtcNow(),
        };
        database.ContentReports.Add(report);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(await database.ContentReports.Where(item => item.Id == report.Id).Select(View).SingleAsync(token),
            Created: true);
    }
}
