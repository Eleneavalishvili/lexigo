using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed record CreateContentReport(Guid SessionExerciseId, string Reason, [Required, MaxLength(1000)] string Comment);
public sealed record CreatePublicNameReport([Required, StringLength(32, MinimumLength = 32)] string CardCode,
    [Required, MaxLength(1000)] string Comment);
public sealed record ReviewContentReport(string Status, [MaxLength(1000)] string? Resolution);
public sealed record ContentReportView(Guid Id, string ResourceType, Guid ResourceId, Guid? VersionId,
    string Reason, string Comment, string Status, string? Resolution, DateTimeOffset CreatedAt);
public sealed record ContentReportCreation(ContentReportView? Report = null, string? Error = null, bool Created = false);

public sealed partial class ContentReportService(LexiGoDbContext database, LearningAccess learning,
    ContentAccess content, TimeProvider clock, RankingReportTokens reportTokens)
{
    private static readonly Expression<Func<ContentReport, ContentReportView>> View = item =>
        new(item.Id, item.ResourceType, item.ResourceType == "RankingName" ? Guid.Empty : item.ResourceId,
            item.VersionId, item.Reason, item.Comment,
            item.Status, item.Resolution, item.CreatedAt);

    public async Task<ContentReportCreation> CreateAsync(Guid userId, Guid familyId, CreateContentReport input, CancellationToken token)
    {
        if (input.Reason is not ("Exercise" or "Audio" or "Text") || !ValidText(input.Comment))
            return new(Error: "Select an issue and describe what happened.");

        await using var transaction = await database.Database.BeginTransactionAsync(token);
        if (!await learning.LockAsync(userId, familyId, token)) return new();
        var resource = await (from exercise in database.SessionExercises
                              join session in database.LearningSessions on exercise.SessionId equals session.Id
                              join course in database.Courses on session.CourseId equals course.Id
                              join source in database.Exercises on exercise.ExerciseId equals source.Id
                              where exercise.Id == input.SessionExerciseId && exercise.UserId == userId && session.UserId == userId
                              select new { exercise.ExerciseId, source.LessonVersionId, course.TargetLanguageId })
            .SingleOrDefaultAsync(token);
        if (resource is null) return new();

        var previous = await database.ContentReports.Where(item => item.ReporterId == userId
            && item.ResourceId == resource.ExerciseId && item.VersionId == resource.LessonVersionId
            && item.Reason == input.Reason).Select(View).SingleOrDefaultAsync(token);
        if (previous is not null)
            return previous.Comment == input.Comment.Trim() ? new(previous) : new(Error: "You already reported this issue on this exercise version.");

        var report = new ContentReport
        {
            ReporterId = userId, LanguageId = resource.TargetLanguageId, ResourceType = "Exercise",
            ResourceId = resource.ExerciseId, VersionId = resource.LessonVersionId, Reason = input.Reason,
            Comment = input.Comment.Trim(), CreatedAt = clock.GetUtcNow()
        };
        database.ContentReports.Add(report);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(await database.ContentReports.Where(item => item.Id == report.Id).Select(View).SingleAsync(token),
            Created: true);
    }

    public async Task<ContentReportCreation> ReportPublicNameAsync(Guid userId, Guid familyId,
        CreatePublicNameReport input, CancellationToken token)
    {
        if (!ValidText(input.Comment)) return new(Error: "Describe what happened.");
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        await AdministrationLocks.AcquireAsync(database, [userId], token);
        if (!await learning.LockAsync(userId, familyId, token)) return new();
        var resource = await (from card in database.CompletionCards
                              join owner in database.Users on card.UserId equals owner.Id
                              join course in database.Courses on card.CourseId equals course.Id
                              where card.ShareCode == input.CardCode && card.IsShared && owner.Id != userId
                                  && owner.Status == "Active" && owner.EmailConfirmed
                              select new { card.Id, course.TargetLanguageId }).SingleOrDefaultAsync(token);
        if (resource is null) return new();
        var previous = await database.ContentReports.Where(item => item.ReporterId == userId
            && item.ResourceId == resource.Id && item.ResourceType == "CompletionCard"
            && item.Reason == "PublicName" && (item.Status == "New" || item.Status == "InReview"))
            .OrderByDescending(item => item.CreatedAt).Select(View).FirstOrDefaultAsync(token);
        if (previous is not null)
            return previous.Comment == input.Comment.Trim()
                ? new(previous)
                : new(Error: "A report for this card is already open.");

        var report = new ContentReport
        {
            ReporterId = userId,
            LanguageId = resource.TargetLanguageId,
            ResourceType = "CompletionCard",
            ResourceId = resource.Id,
            Reason = "PublicName",
            Comment = input.Comment.Trim(),
            CreatedAt = clock.GetUtcNow(),
        };
        database.ContentReports.Add(report);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(await database.ContentReports.Where(item => item.Id == report.Id).Select(View).SingleAsync(token),
            Created: true);
    }

    public Task<ContentReportView[]> MineAsync(Guid userId, int page, CancellationToken token) =>
        PageAsync(database.ContentReports.Where(item => item.ReporterId == userId), page, token);

    public Task<ContentReportView?> MineDetailAsync(Guid userId, Guid id, CancellationToken token) =>
        database.ContentReports.AsNoTracking().Where(item => item.Id == id && item.ReporterId == userId)
            .Select(View).SingleOrDefaultAsync(token);

    public async Task<object?> StaffReadAsync(Guid userId, Guid familyId, Guid? id, int page, CancellationToken token)
    {
        var actor = await content.ActorAsync(userId, familyId, false, token, allowModerator: true);
        if (actor is null) return null;
        var reports = Visible(actor);
        if (id is { } reportId)
            return await reports.Where(item => item.Id == reportId).Select(View).SingleOrDefaultAsync(token);
        return await PageAsync(reports, page, token);
    }

    public async Task<LearningOutcome> ReviewAsync(Guid userId, Guid familyId, Guid id, ReviewContentReport input,
        string traceId, CancellationToken token)
    {
        bool terminal = input.Status is "Resolved" or "Rejected";
        if (input.Status != "InReview" && !terminal || terminal && !ValidText(input.Resolution))
            return new(Error: "Choose a review status and give a resolution for closed reports.");
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        // Match deletion's lock order before a decision can notify or audit another account.
        await AdministrationLocks.AcquireAsync(database, [userId], token);
        var actor = await content.ActorAsync(userId, familyId, true, token, allowModerator: true);
        if (actor is null) return new(Missing: true);
        var report = await Visible(actor).Where(item => item.Id == id).SingleOrDefaultAsync(token);
        if (report is null) return new(Missing: true);

        // Serialize transitions across moderators after checking language scope.
        report = await database.ContentReports.FromSqlInterpolated($"SELECT * FROM \"ContentReports\" WHERE \"Id\" = {id} FOR UPDATE")
            .AsNoTracking().SingleAsync(token);
        string? resolution = terminal ? input.Resolution!.Trim() : null;
        if (report.Status == input.Status && report.Resolution == resolution)
            return new(await database.ContentReports.Where(item => item.Id == id).Select(View).SingleAsync(token));
        if (report.Status is "Resolved" or "Rejected")
            return new(Error: "This report has already been closed.");

        if (report is { Reason: "PublicName", ResourceType: "RankingName" } && input.Status == "Resolved")
        {
            int changed = await database.UserPreferences
                .Where(item => item.UserId == report.ResourceId && item.LeaderboardOptIn)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.LeaderboardOptIn, false)
                    .SetProperty(item => item.SharedLeaderboardAvatarId, (Guid?)null), token);
            if (changed > 0)
                database.Notifications.Add(new Notification
                {
                    UserId = report.ResourceId, Type = "ReportOutcome", Title = "Your ranking participation was paused",
                    Body = "A moderator reviewed your public name. Review your display name before joining again.",
                    CreatedAt = clock.GetUtcNow(),
                });
        }

        if (report is { Reason: "PublicName", ResourceType: "CompletionCard" } && input.Status == "Resolved")
        {
            var card = await database.CompletionCards.FromSqlInterpolated(
                $"SELECT * FROM \"CompletionCards\" WHERE \"Id\" = {report.ResourceId} FOR UPDATE")
                .SingleOrDefaultAsync(token);
            if (card is not null && card.IsShared)
            {
                card.IsShared = false;
                card.ShareCode = null;
                card.Revision++;
                database.Notifications.Add(new Notification
                {
                    UserId = card.UserId, Type = "ReportOutcome", Title = "Your shared card was made private",
                    Body = "A moderator reviewed its public name. Review your display name before sharing again.",
                    CreatedAt = clock.GetUtcNow(),
                });
                database.AuditLogs.Add(new AuditLog
                {
                    ActorId = userId, EntityType = "CompletionCard", EntityId = card.Id.ToString(),
                    Action = "CompletionCardModerated", Reason = input.Resolution!.Trim(),
                    TraceId = traceId, OccurredAt = clock.GetUtcNow(),
                });
            }
        }

        database.Attach(report);
        report.Status = input.Status;
        report.ResolvedBy = terminal ? userId : null;
        report.Resolution = resolution;
        var now = clock.GetUtcNow();
        if (terminal)
            database.Notifications.Add(new Notification
            {
                UserId = report.ReporterId, Type = "ReportOutcome", Title = "Your content report was reviewed",
                Body = "Open your reports to see the decision.", CreatedAt = now
            });
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = userId, EntityType = "ContentReport", EntityId = id.ToString(),
            Action = "ContentReportReviewed", Reason = input.Status, TraceId = traceId, OccurredAt = now
        });
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(await database.ContentReports.Where(item => item.Id == id).Select(View).SingleAsync(token));
    }

    private IQueryable<ContentReport> Visible(ContentActor actor) =>
        database.ContentReports.AsNoTracking().Where(item => actor.IsAdmin || actor.Roles.Contains(RoleNames.Moderator)
            || item.ResourceType == "Exercise" && actor.Roles.Contains(RoleNames.Reviewer)
                && actor.LanguageIds.Contains(item.LanguageId));

    private static Task<ContentReportView[]> PageAsync(IQueryable<ContentReport> query, int page, CancellationToken token) =>
        query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * 20).Take(20).Select(View).ToArrayAsync(token);

    private static bool ValidText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 1000 && !value.Contains('\0');
}
