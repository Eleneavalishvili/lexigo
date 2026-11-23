using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed record ManagedExercise(Guid LessonId, Guid VersionId, StaffExercise Exercise);

public sealed class ManagedExerciseQueries(LexiGoDbContext database, ContentAccess access)
{
    public async Task<ManagedExercise?> GetAsync(Guid userId, Guid familyId, Guid id, CancellationToken token)
    {
        var actor = await access.ActorAsync(userId, familyId, false, token);
        if (actor is null) return null;
        var context = await (from exercise in database.Exercises.AsNoTracking()
                             join version in database.LessonVersions on exercise.LessonVersionId equals version.Id
                             join lesson in database.Lessons on version.LessonId equals lesson.Id
                             join unit in database.Units on lesson.UnitId equals unit.Id
                             join course in database.Courses on unit.CourseId equals course.Id
                             where exercise.Id == id
                             select new { Exercise = exercise, LessonId = lesson.Id, VersionId = version.Id,
                                 lesson.OwnerId, version.Status, course.TargetLanguageId })
            .SingleOrDefaultAsync(token);
        if (context is null || !actor.CanRead(context.TargetLanguageId, context.OwnerId, context.Status)) return null;
        var options = await database.ExerciseOptions.AsNoTracking().Where(item => item.ExerciseId == id)
            .OrderBy(item => item.OrderNumber).Select(item => new StaffOption(item.Id, item.Text, item.OrderNumber,
                item.MatchKey, item.AudioMediaId)).ToArrayAsync(token);
        var answers = await database.AcceptedAnswers.AsNoTracking().Where(item => item.ExerciseId == id)
            .OrderBy(item => item.Id).Select(item => new StaffAnswer(item.Id, item.AnswerText, item.OrderedTokenIds))
            .ToArrayAsync(token);
        var skills = await database.ExerciseSkills.AsNoTracking().Where(item => item.ExerciseId == id)
            .OrderBy(item => item.SkillId).Select(item => item.SkillId).ToArrayAsync(token);
        var row = context.Exercise;
        return new(context.LessonId, context.VersionId, new StaffExercise(row.Id, row.Type, row.Prompt,
            row.Explanation, row.OrderNumber, row.AudioMediaId, row.EvaluationRules, options, answers, row.ContextText, skills));
    }
}
