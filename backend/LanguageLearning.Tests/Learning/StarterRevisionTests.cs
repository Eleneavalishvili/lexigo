using System.Net.Http.Json;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class StarterRevisionTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task PublishingReorderedContentPreservesAnAlreadyStartedSession()
    {
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var pack = StarterLessonInstaller.Read("es");
        (await database.Courses.SingleAsync(item => item.Id == pack.CourseId)).IsPublished = true;
        var unit = new Unit
        {
            Id = StarterLessonInstaller.ContentId("es/unit"),
            CourseId = pack.CourseId,
            Title = "First conversations",
            Description = "Original unit",
            OrderNumber = 1
        };
        var lesson = new Lesson
        {
            Id = StarterLessonInstaller.ContentId("es/greetings"),
            UnitId = unit.Id,
            OwnerId = learner.Id,
            Title = "Original greeting",
            Objective = "Write hello",
            OrderNumber = 1,
            Status = "Published"
        };
        var version = new LessonVersion
        {
            Id = StarterLessonInstaller.ContentId("es/greetings/version-1"),
            LessonId = lesson.Id,
            VersionNumber = 1,
            CreatedBy = learner.Id,
            Title = lesson.Title,
            Objective = lesson.Objective,
            Explanation = "Hola — Hello",
            Status = "Published",
            PublishedAt = DateTimeOffset.UtcNow
        };
        var exercise = new Exercise
        {
            LessonVersionId = version.Id,
            Type = "Translation",
            Prompt = "Translate hello",
            Explanation = "Hola means hello.",
            OrderNumber = 1,
            EvaluationRules = TextEvaluationRulesCodec.Write(new TextEvaluationRules(true, true))
        };
        database.AddRange(unit, lesson, version, exercise,
            new AcceptedAnswer { ExerciseId = exercise.Id, AnswerText = "Hola" });
        await database.SaveChangesAsync();
        lesson.CurrentVersionId = version.Id;
        await database.SaveChangesAsync();
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await learner.LoginAsync(browser);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { pack.CourseId }, token)).EnsureSuccessStatusCode();
        var start = await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.Id}/sessions", bearer: token);
        start.EnsureSuccessStatusCode();
        var original = (await start.Content.ReadFromJsonAsync<SessionView>())!;

        var installer = scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>();
        await installer.InstallAsync(CancellationToken.None);
        Assert.Equal(0, await installer.InstallAsync(CancellationToken.None));
        Assert.NotEqual(version.Id, lesson.CurrentVersionId);
        Assert.True(await database.LessonVersions.AnyAsync(item => item.Id == version.Id));
        var reopened = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{original.Id}", bearer: token);
        reopened.EnsureSuccessStatusCode();
        var saved = (await reopened.Content.ReadFromJsonAsync<SessionView>())!;
        Assert.Equal(version.Id, saved.VersionId);
        Assert.Equal("Translate hello", saved.CurrentExercise!.Prompt);
        Assert.Equal(1, saved.TotalCount);
        var answer = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{saved.Id}/answers",
            new { sessionExerciseId = saved.CurrentExercise.Id, answerText = "Hola", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, token);
        answer.EnsureSuccessStatusCode();
        Assert.True((await answer.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect);
    }
}
