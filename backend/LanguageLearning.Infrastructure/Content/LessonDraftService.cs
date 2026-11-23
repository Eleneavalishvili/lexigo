using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class LessonDraftService(LexiGoDbContext database, ContentAccess access, ContentTransactions content)
{
    public async Task<ContentOutcome> CreateAsync(Guid actorId, Guid familyId, LessonInput input, string traceId, CancellationToken cancellationToken)
    {
        if (!ValidText(input.Title, 160) || !ValidText(input.Objective, 1000) || input.Explanation.Length > 20000 || input.OrderNumber < 1) return new(Error: "Check lesson fields.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        var unit = await database.Units.SingleOrDefaultAsync(item => item.Id == input.UnitId, cancellationToken);
        if (actor is null || unit is null) return new(Forbidden: true);
        var course = await content.LockCourseAsync(unit.CourseId, cancellationToken);
        if (course is null || !actor.CanAuthor(course.TargetLanguageId)) return new(Forbidden: true);
        if (await database.Lessons.AnyAsync(item => item.UnitId == unit.Id && item.OrderNumber == input.OrderNumber, cancellationToken)) return new(Error: "This lesson position is already occupied.");
        var lesson = new Lesson { UnitId = unit.Id, OwnerId = actorId, Title = input.Title.Trim(), Objective = input.Objective.Trim(), OrderNumber = input.OrderNumber };
        database.Lessons.Add(lesson);
        await database.SaveChangesAsync(cancellationToken);
        database.LessonVersions.Add(new LessonVersion { LessonId = lesson.Id, VersionNumber = 1, Title = lesson.Title, Objective = lesson.Objective, Explanation = input.Explanation, CreatedBy = actorId });
        content.Audit(actorId, lesson.Id, "LessonDraftCreated", "Initial draft created.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(lesson.Id);
    }

    public async Task<ContentOutcome> EditAsync(Guid actorId, Guid familyId, Guid lessonId, DraftInput input, string traceId, CancellationToken cancellationToken)
    {
        if (!ValidText(input.Title, 160) || !ValidText(input.Objective, 1000) || input.Explanation.Length > 20000) return new(Error: "Check lesson fields.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var state = await content.LessonAsync(actorId, familyId, lessonId, cancellationToken);
        if (state is not { } context || !context.Actor.CanEdit(context.Course.TargetLanguageId, context.Lesson.OwnerId)) return new(Forbidden: true);
        if (context.Version.Status != "Draft" || context.Version.Id != input.VersionId) return new(Error: "Only the current draft can be edited. Start a revision for published material.");
        context.Version.Title = input.Title.Trim(); context.Version.Objective = input.Objective.Trim(); context.Version.Explanation = input.Explanation;
        context.Lesson.Title = context.Version.Title; context.Lesson.Objective = context.Version.Objective;
        content.Audit(actorId, lessonId, "LessonDraftEdited", "Draft explanation and metadata updated.", traceId);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(lessonId);
    }

    public async Task<ContentOutcome> MoveAsync(Guid actorId, Guid familyId, Guid lessonId, int fromPosition,
        int toPosition, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var state = await content.LessonAsync(actorId, familyId, lessonId, cancellationToken);
        if (state is not { } context || !context.Actor.IsAdmin) return new(Forbidden: true);
        var lesson = context.Lesson;
        if (lesson.OrderNumber != fromPosition)
            return new(Error: "The lesson order changed. Reload the lesson and try again.");
        if (fromPosition == toPosition) return new(lessonId);

        var neighbor = await database.Lessons.SingleOrDefaultAsync(item => item.UnitId == lesson.UnitId
            && item.OrderNumber == toPosition, cancellationToken);
        if (neighbor is not null)
        {
            var positions = await database.Lessons.Where(item => item.UnitId == lesson.UnitId)
                .Select(item => item.OrderNumber).ToArrayAsync(cancellationToken);
            var occupied = positions.ToHashSet();
            lesson.OrderNumber = Enumerable.Range(1, positions.Length + 1).First(position => !occupied.Contains(position));
            await database.SaveChangesAsync(cancellationToken);
            neighbor.OrderNumber = fromPosition;
            await database.SaveChangesAsync(cancellationToken);
            content.Audit(actorId, neighbor.Id, "LessonMoved", "Lesson position swapped within its unit.", traceId);
        }
        lesson.OrderNumber = toPosition;
        content.Audit(actorId, lesson.Id, "LessonMoved", "Lesson position changed within its unit.", traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(lessonId);
    }

    private static bool ValidText(string text, int maximum) => !string.IsNullOrWhiteSpace(text) && text.Length <= maximum && !text.Contains('\0');
}
