using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed record ValidateLessonImport(Guid UnitId, int OrderNumber, [Required, MaxLength(160)] string Title,
    [Required, MaxLength(1000)] string Objective, [Required, MaxLength(20000)] string Explanation,
    [Required, MaxLength(64000)] string Csv);
public sealed record LessonImportReview(Guid Id, string Status, int ExerciseCount, ImportIssue[] Errors);
public sealed record LessonImportJobStatus(Guid Id, string Status, ImportIssue[] Errors, DateTimeOffset CreatedAt,
    Guid? LessonId, string Title, int? ExerciseCount);
public sealed record LessonImportResult(LessonImportReview? Review = null, Guid? LessonId = null,
    bool Forbidden = false, bool Missing = false, string? Error = null, bool Created = false);

public sealed class LessonImports(LexiGoDbContext database, ContentAccess access, ContentTransactions content, TimeProvider clock)
{
    private sealed record StagedLesson(Guid UnitId, int OrderNumber, string Title, string Objective,
        string Explanation, ImportedExercise[] Exercises);

    public async Task<LessonImportJobStatus?> GetAsync(Guid actorId, Guid family, Guid id, CancellationToken token)
    {
        var actor = await access.ActorAsync(actorId, family, false, token);
        if (actor is null) return null;
        var job = await database.ImportJobs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.AuthorId == actorId, token);
        if (job is null) return null;
        var staged = JsonSerializer.Deserialize<StagedLesson>(job.Payload)!;
        var target = await TargetAsync(staged.UnitId, token);
        if (target is null || !actor.CanAuthor(target.Value.LanguageId)) return null;
        return new(job.Id, job.Status, JsonSerializer.Deserialize<ImportIssue[]>(job.ValidationErrors)!, job.CreatedAt,
            job.LessonId, staged.Title, job.Status == "Invalid" ? null : staged.Exercises.Length);
    }

    public async Task<LessonImportResult> ValidateAsync(Guid actorId, Guid family, ValidateLessonImport input, CancellationToken token)
    {
        if (input.OrderNumber < 1 || !LessonImportCsv.Valid(input.Title, 160) || !LessonImportCsv.Valid(input.Objective, 1000)
            || !LessonImportCsv.Valid(input.Explanation, 20000) || input.Csv is not { Length: > 0 and <= 64000 })
            return new(Error: "Enter a title, objective, explanation, lesson position and CSV file.");

        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var actor = await access.ActorAsync(actorId, family, true, token);
        var target = await TargetAsync(input.UnitId, token);
        if (actor is null || target is null || !actor.CanAuthor(target.Value.LanguageId)) return new(Forbidden: true);
        var (exercises, errors) = LessonImportCsv.Parse(input.Csv);
        if (await PositionTakenAsync(input.UnitId, input.OrderNumber, token))
            errors = [.. errors, new(0, "This lesson position is already occupied.")];

        var job = new ImportJob
        {
            AuthorId = actorId,
            Status = errors.Length == 0 ? "Valid" : "Invalid",
            FileKey = "",
            ValidationErrors = JsonSerializer.Serialize(errors),
            Payload = JsonSerializer.Serialize(new StagedLesson(input.UnitId, input.OrderNumber,
                input.Title.Trim(), input.Objective.Trim(), input.Explanation.Trim(), errors.Length == 0 ? exercises : [])),
            CreatedAt = clock.GetUtcNow()
        };
        job.FileKey = $"database:lesson-import/{job.Id:N}";
        database.ImportJobs.Add(job);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(new(job.Id, job.Status, exercises.Length, errors));
    }

    public async Task<LessonImportResult> CommitAsync(Guid actorId, Guid family, Guid id, string traceId, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var actor = await access.ActorAsync(actorId, family, true, token);
        if (actor is null) return new(Forbidden: true);
        var job = await database.ImportJobs.SingleOrDefaultAsync(item => item.Id == id && item.AuthorId == actorId, token);
        if (job is null) return new(Missing: true);
        var staged = JsonSerializer.Deserialize<StagedLesson>(job.Payload)!;
        var target = await TargetAsync(staged.UnitId, token);
        if (target is null) return new(Missing: true);
        var course = await content.LockCourseAsync(target.Value.CourseId, token);
        if (course is null || !actor.CanAuthor(course.TargetLanguageId)) return new(Forbidden: true);
        if (job.LessonId is { } saved) return new(LessonId: saved);
        if (job.Status != "Valid") return new(Error: "Correct the validation errors and validate a new import.");
        if (await PositionTakenAsync(staged.UnitId, staged.OrderNumber, token))
            return new(Error: "This lesson position is already occupied. Validate again with a free position.");

        var lesson = new Lesson
        {
            UnitId = staged.UnitId, OwnerId = actorId, Title = staged.Title,
            Objective = staged.Objective, OrderNumber = staged.OrderNumber
        };
        var version = new LessonVersion
        {
            LessonId = lesson.Id, VersionNumber = 1, CreatedBy = actorId,
            Title = lesson.Title, Objective = lesson.Objective, Explanation = staged.Explanation
        };
        database.Lessons.Add(lesson);
        database.LessonVersions.Add(version);
        for (int index = 0; index < staged.Exercises.Length; index++)
        {
            var row = staged.Exercises[index];
            var exercise = new Exercise
            {
                LessonVersionId = version.Id, Type = "Translation", OrderNumber = index + 1,
                Prompt = row.Prompt, Explanation = row.Feedback
            };
            database.Exercises.Add(exercise);
            database.AcceptedAnswers.Add(new AcceptedAnswer { ExerciseId = exercise.Id, AnswerText = row.Answer });
        }

        job.LessonId = lesson.Id;
        job.Status = "Committed";
        job.Payload = JsonSerializer.Serialize(staged with { Exercises = [] });
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId, EntityType = "ImportJob", EntityId = id.ToString(),
            Action = "LessonImportCommitted", Reason = $"Created draft lesson {lesson.Id} from validated CSV.",
            TraceId = traceId, OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(LessonId: lesson.Id, Created: true);
    }

    private async Task<(Guid CourseId, int LanguageId)?> TargetAsync(Guid unitId, CancellationToken token)
    {
        var target = await (from unit in database.Units
                            join course in database.Courses on unit.CourseId equals course.Id
                            where unit.Id == unitId
                            select new { course.Id, course.TargetLanguageId }).SingleOrDefaultAsync(token);
        return target is null ? null : (target.Id, target.TargetLanguageId);
    }

    private Task<bool> PositionTakenAsync(Guid unitId, int position, CancellationToken token) =>
        database.Lessons.AnyAsync(item => item.UnitId == unitId && item.OrderNumber == position, token);
}
