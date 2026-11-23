using System.Data;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed record CourseUsage(Guid CourseId, string Course, int Learners, int Started, int Finished);
public sealed record ExerciseDifficulty(string Format, int Answers, int Errors);
public sealed record CommonError(Guid ExerciseId, string Format, int Answers, int Errors);
public sealed record LearningAnalyticsView(int ActiveLearners, CourseUsage[] Courses,
    ExerciseDifficulty[] Difficulty, CommonError[] CommonErrors);

public sealed class LearningAnalytics(LexiGoDbContext database, ContentAccess access)
{
    public async Task<LearningAnalyticsView?> ReadAsync(Guid userId, Guid familyId, Guid? courseId,
        DateTimeOffset start, DateTimeOffset end, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        if (await access.ActorAsync(userId, familyId, false, token) is not { IsAdmin: true }) return null;
        var sessions = database.LearningSessions.AsNoTracking().Where(item =>
            courseId == null || item.CourseId == courseId);
        var started = sessions.Where(item => item.StartedAt >= start && item.StartedAt < end);
        var active = await started.Select(item => item.UserId).Distinct().CountAsync(token);
        var usage = await (from session in started
                           join course in database.Courses on session.CourseId equals course.Id
                           group session by new { course.Id, course.Title } into course
                           orderby course.Key.Title, course.Key.Id
                           select new CourseUsage(course.Key.Id, course.Key.Title,
                               course.Select(item => item.UserId).Distinct().Count(), course.Count(),
                               course.Count(item => item.Mode == "Lesson" && item.CompletedAt != null && item.CompletedAt < end)))
            .ToArrayAsync(token);

        var answers = from attempt in database.AnswerAttempts
                      join question in database.SessionExercises on attempt.SessionExerciseId equals question.Id
                      join session in sessions on question.SessionId equals session.Id
                      join exercise in database.Exercises on question.ExerciseId equals exercise.Id
                      where attempt.IsGraded && attempt.SubmittedAt >= start && attempt.SubmittedAt < end
                      select new { exercise.Id, exercise.Type, attempt.IsCorrect };

        var difficulty = await answers.GroupBy(item => item.Type).OrderBy(group => group.Key)
            .Select(group => new ExerciseDifficulty(group.Key, group.Count(), group.Count(item => !item.IsCorrect)))
            .ToArrayAsync(token);
        var errors = await answers.GroupBy(item => new { item.Id, item.Type })
            .Select(group => new { group.Key.Id, group.Key.Type, Answers = group.Count(), Errors = group.Count(item => !item.IsCorrect) })
            .Where(item => item.Errors > 0).OrderByDescending(item => item.Errors).ThenBy(item => item.Id)
            .Take(25).Select(item => new CommonError(item.Id, item.Type, item.Answers, item.Errors))
            .ToArrayAsync(token);
        await transaction.CommitAsync(token);
        return new(active, usage, difficulty, errors);
    }
}
