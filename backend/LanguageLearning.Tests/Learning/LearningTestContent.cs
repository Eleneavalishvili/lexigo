using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Tests.Learning;

internal sealed record LearningTestContent(Guid CourseId, Guid LessonId, Guid VersionId, Guid[] ExerciseIds)
{
    public static async Task<LearningTestContent> CreateAsync(RegistrationFixture fixture, Guid ownerId, int count = 2, string type = "Translation", int courseNumber = 1)
    {
        // Curated fixture data in Testcontainers only; never publish synthetic material to the real application.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid courseId = Guid.Parse($"10000000-0000-0000-0000-{courseNumber:D12}");
        var course = await database.Courses.SingleAsync(item => item.Id == courseId);
        course.IsPublished = true;
        int order = (await database.Units.Where(item => item.CourseId == courseId).Select(item => (int?)item.OrderNumber).MaxAsync() ?? 0) + 1;
        var unit = new Unit { CourseId = course.Id, Title = "Greetings", Description = "First words", Difficulty = "Beginner", OrderNumber = order };
        var lesson = new Lesson { UnitId = unit.Id, OwnerId = ownerId, Title = "Hello", Objective = "Greet someone", OrderNumber = 1, Status = "Published" };
        var version = new LessonVersion { LessonId = lesson.Id, CreatedBy = ownerId, Title = "Hello", Objective = "Greet someone", Explanation = "English lesson notes", VersionNumber = 1, Status = "Published", PublishedAt = DateTimeOffset.UtcNow };
        database.AddRange(unit, lesson, version);
        var ids = new List<Guid>();
        for (int index = 1; index <= count; index++)
        {
            var exercise = new Exercise { LessonVersionId = version.Id, Type = type, Prompt = $"Translate hello ({index})", Explanation = "Hola is a greeting.", OrderNumber = index };
            database.Exercises.Add(exercise); ids.Add(exercise.Id);
            database.AcceptedAnswers.Add(new AcceptedAnswer { ExerciseId = exercise.Id, AnswerText = "Hola" });
            if (type == "MultipleChoice")
            {
                database.ExerciseOptions.Add(new ExerciseOption { ExerciseId = exercise.Id, Text = "Hola", OrderNumber = 1 });
                database.ExerciseOptions.Add(new ExerciseOption { ExerciseId = exercise.Id, Text = "Adiós", OrderNumber = 2 });
            }
        }
        await database.SaveChangesAsync();
        lesson.CurrentVersionId = version.Id;
        await database.SaveChangesAsync();
        return new(course.Id, lesson.Id, version.Id, ids.ToArray());
    }
}
