using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class PrerequisiteService(LexiGoDbContext database, ContentTransactions content)
{
    public async Task<ContentOutcome> SaveAsync(Guid actorId, Guid familyId, Guid lessonId, Guid[] prerequisites, string traceId, CancellationToken cancellationToken)
    {
        if (prerequisites.Length > 50 || prerequisites.Distinct().Count() != prerequisites.Length || prerequisites.Contains(lessonId)) return new(Error: "Invalid prerequisites.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var state = await content.LessonAsync(actorId, familyId, lessonId, cancellationToken);
        if (state is not { } context || !context.Actor.CanEdit(context.Course.TargetLanguageId, context.Lesson.OwnerId)) return new(Forbidden: true);
        if (context.Lesson.CurrentVersionId is not null || context.Version.Status != "Draft") return new(Error: "Set prerequisites before the first submission/publication.");
        var lessons = await (from lesson in database.Lessons join unit in database.Units on lesson.UnitId equals unit.Id where unit.CourseId == context.Course.Id select lesson.Id).Take(5001).ToArrayAsync(cancellationToken);
        if (lessons.Length > 5000) return new(Error: "This course exceeds the current prerequisite editing limit.");
        if (prerequisites.Except(lessons).Any()) return new(Error: "Prerequisites must belong to the same course.");
        var edges = await database.LessonPrerequisites.Where(item => lessons.Contains(item.LessonId) && item.LessonId != lessonId).ToArrayAsync(cancellationToken);
        var graph = edges.GroupBy(item => item.LessonId).ToDictionary(group => group.Key, group => group.Select(item => item.PrerequisiteLessonId).ToArray());
        var pending = new Stack<Guid>(prerequisites); var visited = new HashSet<Guid>();
        while (pending.TryPop(out Guid id))
        {
            if (id == lessonId) return new(Error: "Prerequisites cannot create a cycle.");
            if (visited.Add(id) && graph.TryGetValue(id, out var parents)) foreach (Guid parent in parents) pending.Push(parent);
        }
        database.LessonPrerequisites.RemoveRange(await database.LessonPrerequisites.Where(item => item.LessonId == lessonId).ToArrayAsync(cancellationToken));
        await database.SaveChangesAsync(cancellationToken);
        database.LessonPrerequisites.AddRange(prerequisites.Select(id => new LessonPrerequisite { LessonId = lessonId, PrerequisiteLessonId = id }));
        content.Audit(actorId, lessonId, "LessonPrerequisitesChanged", "Prerequisites validated within the course without cycles.", traceId);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(lessonId);
    }
}
