using System.ComponentModel.DataAnnotations;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed record CreateCourseRequest(
    [Range(1, int.MaxValue)] int SourceLanguageId,
    [Range(1, int.MaxValue)] int TargetLanguageId,
    [Required, StringLength(120)] string Title,
    [Required, StringLength(1000)] string Description);

public sealed record CourseLanguage(int Id, string Code, string EnglishName);
public sealed record StaffCourseDetail(Guid Id, int SourceLanguageId, int TargetLanguageId,
    string Title, string Description, int OrderNumber, bool IsPublished, DateTimeOffset? ArchivedAt);

public sealed class CourseCreation(LexiGoDbContext database, ContentAccess access, TimeProvider clock)
{
    public async Task<ContentOutcome> CreateAsync(Guid actorId, Guid familyId,
        CreateCourseRequest request, string traceId, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var actor = await access.ActorAsync(actorId, familyId, true, token);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description)
            || request.Title.Contains('\0') || request.Description.Contains('\0')
            || request.Title.Length > 120 || request.Description.Length > 1000)
            return new(Error: "Check the course title and description.");
        if (request.SourceLanguageId == request.TargetLanguageId
            || await database.Languages.CountAsync(language => language.Id == request.SourceLanguageId
                || language.Id == request.TargetLanguageId, token) != 2)
            return new(Error: "Choose two different registered languages.");

        // Serialize catalog inserts even when no course exists for this pair yet.
        // Row locks alone cannot protect an absent language pair.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730027030)", token);
        if (await database.Courses.AnyAsync(course => course.SourceLanguageId == request.SourceLanguageId
            && course.TargetLanguageId == request.TargetLanguageId, token))
            return new(Error: "A course already exists for these languages. Edit or restore that course instead.");
        var course = new Course
        {
            Id = Guid.NewGuid(),
            SourceLanguageId = request.SourceLanguageId,
            TargetLanguageId = request.TargetLanguageId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            IsPublished = false,
            OrderNumber = (await database.Courses.Select(item => (int?)item.OrderNumber).MaxAsync(token) ?? 0) + 1
        };
        database.Courses.Add(course);
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            EntityType = "Course",
            EntityId = course.Id.ToString(),
            Action = "CourseCreated",
            Reason = "Unpublished course created.",
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(course.Id);
    }

    public async Task<StaffCourseDetail?> DetailAsync(Guid actorId, Guid familyId, Guid id, CancellationToken token)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, token);
        if (actor is null) return null;
        return await database.Courses.AsNoTracking()
            .Where(course => course.Id == id && (actor.IsAdmin || actor.LanguageIds.Contains(course.TargetLanguageId)))
            .Select(course => new StaffCourseDetail(course.Id, course.SourceLanguageId, course.TargetLanguageId,
                course.Title, course.Description, course.OrderNumber, course.IsPublished, course.ArchivedAt))
            .SingleOrDefaultAsync(token);
    }

    public async Task<CourseLanguage[]?> LanguagesAsync(Guid actorId, Guid familyId, CancellationToken token)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, token);
        if (actor is null || !actor.IsAdmin) return null;
        return await database.Languages.AsNoTracking().OrderBy(language => language.EnglishName)
            .Select(language => new CourseLanguage(language.Id, language.Code, language.EnglishName))
            .Take(200).ToArrayAsync(token);
    }
}
