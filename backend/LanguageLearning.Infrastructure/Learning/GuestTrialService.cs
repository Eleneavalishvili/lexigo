using System.Data;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed record TrialOption(Guid Id, string Text);
public sealed record TrialQuestion(Guid Id, string Prompt, TrialOption[] Options);
public sealed record GuestTrial(Guid CourseId, Guid VersionId, string Title, string Explanation, TrialQuestion[] Questions);
public sealed record TrialAnswer(Guid VersionId, Guid QuestionId, Guid OptionId);
public sealed record TrialFeedback(bool IsCorrect, string CorrectAnswer, string Explanation);

public sealed class GuestTrialService(LexiGoDbContext database)
{
    public async Task<GuestTrial?> ReadAsync(Guid courseId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        return await PublishedTrialAsync(courseId, cancellationToken);
    }

    public async Task<TrialFeedback?> CheckAsync(Guid courseId, TrialAnswer input, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var trial = await PublishedTrialAsync(courseId, cancellationToken);
        if (trial is null || trial.VersionId != input.VersionId) return null;
        var question = trial.Questions.SingleOrDefault(item => item.Id == input.QuestionId);
        var option = question?.Options.SingleOrDefault(item => item.Id == input.OptionId);
        if (question is null || option is null) return null;
        var answers = await database.AcceptedAnswers.AsNoTracking().Where(item => item.ExerciseId == question.Id)
            .OrderBy(item => item.Id).Select(item => item.AnswerText).ToArrayAsync(cancellationToken);
        if (answers.Length == 0) return null;
        var explanation = await database.Exercises.Where(item => item.Id == question.Id)
            .Select(item => item.Explanation).SingleAsync(cancellationToken);
        return new(answers.Contains(option.Text, StringComparer.Ordinal), answers[0], explanation);
    }

    private async Task<GuestTrial?> PublishedTrialAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var lesson = await (
            from course in database.Courses.AsNoTracking()
            join unit in database.Units on course.Id equals unit.CourseId
            join item in database.Lessons on unit.Id equals item.UnitId
            join version in database.LessonVersions on item.CurrentVersionId equals version.Id
            where course.Id == courseId && course.IsPublished && unit.ArchivedAt == null && item.Status == "Published"
                && version.Status == "Published" && version.LessonId == item.Id
                && version.Explanation.Contains(" — ")
                && database.Exercises.Any(exercise => exercise.LessonVersionId == version.Id
                    && exercise.Type == "MultipleChoice" && exercise.AudioMediaId == null)
            orderby unit.OrderNumber, item.OrderNumber
            select new { version.Id, version.Title, version.Explanation }).FirstOrDefaultAsync(cancellationToken);
        if (lesson is null) return null;
        var exercises = await database.Exercises.AsNoTracking()
            .Where(item => item.LessonVersionId == lesson.Id && item.Type == "MultipleChoice" && item.AudioMediaId == null)
            .OrderBy(item => item.OrderNumber).Take(3).Select(item => new { item.Id, item.Prompt }).ToArrayAsync(cancellationToken);
        if (exercises.Length == 0) return null;
        var ids = exercises.Select(item => item.Id).ToArray();
        var options = await database.ExerciseOptions.AsNoTracking().Where(item => ids.Contains(item.ExerciseId))
            .OrderBy(item => item.OrderNumber).Select(item => new { item.ExerciseId, item.Id, item.Text }).ToArrayAsync(cancellationToken);
        var questions = exercises.Select(item => new TrialQuestion(item.Id, item.Prompt,
            options.Where(option => option.ExerciseId == item.Id).Select(option => new TrialOption(option.Id, option.Text))
                .OrderBy(_ => Random.Shared.Next()).ToArray())).ToArray();
        return new(courseId, lesson.Id, lesson.Title, lesson.Explanation, questions);
    }
}
