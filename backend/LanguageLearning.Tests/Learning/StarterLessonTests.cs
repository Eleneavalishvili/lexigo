using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Vocabulary;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed partial class StarterLessonTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Theory]
    [InlineData("es")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("ru")]
    public async Task CalendarAndStudyLessonsAcceptNaturalEnglishAlternatives(string code)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>().InstallAsync(default);
        var account = await SecurityTestAccount.CreateAsync(fixture);
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        // Prerequisite setup only; the complete-course test separately verifies its actual learning path.
        database.LessonProgress.Add(new LanguageLearning.Core.Learning.LessonProgress
        {
            UserId = account.Id,
            LessonId = StarterLessonInstaller.ContentId(code + "/greetings"),
            Status = "Completed",
            BestScore = 100,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync();
        var pack = StarterLessonInstaller.Read(code);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await account.LoginAsync(browser);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { pack.CourseId }, token))
            .EnsureSuccessStatusCode();
        foreach (var source in pack.Lessons.Where(item => item.UnitKey == "dates-work-study" && !item.IsCheckpoint))
        {
            Assert.Contains(source.Exercises, question => question.Answers.Length > 1);
            var alternativeSource = source with
            {
                Exercises = source.Exercises.Select(question => question with
                {
                    Answers = question.Answers.Reverse().ToArray()
                }).ToArray()
            };
            await CompleteLessonAsync(browser, token, StarterLessonInstaller.ContentId(code + "/" + source.Key), alternativeSource);
        }
    }

    [Fact]
    public async Task AFreeLearnerKeepsSeparateProgressAndCanSaveWordsAndPractiseInAllFourCourses()
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>().InstallAsync(default);
        var account = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await account.LoginAsync(browser);
        var entitlements = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/entitlements", bearer: token);
        entitlements.EnsureSuccessStatusCode();
        var access = (await entitlements.Content.ReadFromJsonAsync<EntitlementView>())!;
        Assert.Equal("FREE", access.PlanCode);
        Assert.Contains(access.Features, feature => feature.Code == "CoreListening" && feature.Available);
        var completed = new Dictionary<Guid, Guid>();

        foreach (string language in new[] { "es", "it", "de", "ru" })
        {
            var pack = StarterLessonInstaller.Read(language);
            var source = pack.Lessons[0];
            Guid lessonId = StarterLessonInstaller.ContentId(language + "/" + source.Key);
            (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments",
                new { pack.CourseId }, token)).EnsureSuccessStatusCode();
            await CompleteLessonAsync(browser, token, lessonId, source);
            completed.Add(pack.CourseId, lessonId);

            var words = await browser.SendAsync(HttpMethod.Get, $"/api/v1/vocabulary?courseId={pack.CourseId}", bearer: token);
            words.EnsureSuccessStatusCode();
            var word = (await words.Content.ReadFromJsonAsync<WordPage>())!.Items.First();
            (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words",
                new { vocabularyItemId = word.Id }, token)).EnsureSuccessStatusCode();
            var saved = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/saved-words?courseId={pack.CourseId}", bearer: token);
            saved.EnsureSuccessStatusCode();
            Assert.Equal(word.Id, Assert.Single((await saved.Content.ReadFromJsonAsync<WordPage>())!.Items).Id);

            var practice = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions",
                new { lessonId, mistakesOnly = false }, token);
            practice.EnsureSuccessStatusCode();
            Assert.Equal("Practice", (await practice.Content.ReadFromJsonAsync<SessionView>())!.Mode);
        }

        foreach (var (courseId, lessonId) in completed)
        {
            var response = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/progress?courseId={courseId}", bearer: token);
            response.EnsureSuccessStatusCode();
            var progress = (await response.Content.ReadFromJsonAsync<CourseProgress>())!;
            Assert.True(progress.IsEnrolled);
            Assert.Equal(lessonId, Assert.Single(progress.Lessons, lesson => lesson.Status == "Completed").Id);
        }
    }

    [Fact]
    public async Task StartingLevelIsSavedAsSelfReportedAndDoesNotUnlockOrAwardAnything()
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>().InstallAsync(CancellationToken.None);
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await learner.LoginAsync(browser);
        Guid courseId = StarterLessonInstaller.Read("es").CourseId;
        var joined = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId, startingLevel = "C2" }, token);
        joined.EnsureSuccessStatusCode();
        var response = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/progress?courseId={courseId}", bearer: token);
        response.EnsureSuccessStatusCode();
        var progress = (await response.Content.ReadFromJsonAsync<CourseProgress>())!;
        Assert.Equal("C2", progress.StartingLevel);
        Assert.Single(progress.Lessons, item => item.IsUnlocked);
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal("Self:C2", (await database.Enrollments.SingleAsync(item => item.UserId == learner.Id && item.CourseId == courseId)).PlacementLevel);
        Assert.False(await database.LessonProgress.AnyAsync(item => item.UserId == learner.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId, startingLevel = "SuperAdmin" }, token)).StatusCode);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId, startingLevel = "A1" }, token)).EnsureSuccessStatusCode();
        Assert.Equal(1, await database.Enrollments.CountAsync(item => item.UserId == learner.Id && item.CourseId == courseId));
    }

    [Fact]
    public async Task WrongAnswerRevealsItsCorrectionAfterSubmissionAndSurvivesReload()
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>().InstallAsync(CancellationToken.None);
        var account = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await account.LoginAsync(browser);
        Guid courseId = StarterLessonInstaller.Read("es").CourseId;
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId }, token)).EnsureSuccessStatusCode();
        var firstSource = StarterLessonInstaller.Read("es").Lessons[0];
        Guid lesson = StarterLessonInstaller.ContentId("es/" + firstSource.Key);
        var start = await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson}/sessions", bearer: token);
        start.EnsureSuccessStatusCode();
        var session = (await start.Content.ReadFromJsonAsync<SessionView>())!;
        using var json = System.Text.Json.JsonDocument.Parse(await start.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("currentExercise").TryGetProperty("correctAnswer", out _));
        var response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new
            {
                sessionExerciseId = session.CurrentExercise!.Id,
                answerText = session.CurrentExercise.Options.Length == 0 ? "An incorrect answer" : null,
                selectedOptionIds = session.CurrentExercise.Options.Where(item => item.Text != firstSource.Exercises[0].Answers[0]).Take(1).Select(item => item.Id).ToArray(),
                idempotencyKey = Guid.NewGuid()
            }, token);
        response.EnsureSuccessStatusCode();
        var feedback = (await response.Content.ReadFromJsonAsync<AnswerFeedback>())!;
        Assert.False(feedback.IsCorrect);
        Assert.Contains(feedback.CorrectAnswer!, StarterLessonInstaller.Read("es").Lessons[0].Exercises[0].Answers);
        Assert.Equal(session.CurrentExercise.Prompt, feedback.Prompt);
        Assert.Equal(4, feedback.HeartsRemaining);
        var reopened = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: token);
        reopened.EnsureSuccessStatusCode();
        Assert.Equal(feedback, (await reopened.Content.ReadFromJsonAsync<SessionView>())!.LastFeedback!.Feedback);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("ru")]
    public async Task RealStarterContentCanBeResumedCompletedAndUnlocksTheNextLesson(string code)
    {
        using var scope = fixture.Services.CreateScope();
        var installer = scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>();
        await installer.InstallAsync(CancellationToken.None);
        Assert.Equal(0, await installer.InstallAsync(CancellationToken.None));
        var pack = StarterLessonInstaller.Read(code);
        var account = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await account.LoginAsync(browser);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { pack.CourseId }, token)).EnsureSuccessStatusCode();
        Guid lessonId = StarterLessonInstaller.ContentId(code + "/greetings");
        Guid nextId = StarterLessonInstaller.ContentId(code + "/introductions");
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{nextId}/sessions", bearer: token)).StatusCode);
        var firstCheck = pack.Lessons.First(item => item.IsCheckpoint);
        Guid checkId = StarterLessonInstaller.ContentId(code + "/" + firstCheck.Key);
        Assert.Equal(HttpStatusCode.Conflict,
            (await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{checkId}/sessions", bearer: token)).StatusCode);
        var initialProgress = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/progress?courseId={pack.CourseId}", bearer: token);
        initialProgress.EnsureSuccessStatusCode();
        var lockedCheck = (await initialProgress.Content.ReadFromJsonAsync<CourseProgress>())!.Lessons.Single(item => item.Id == checkId);
        Assert.False(lockedCheck.IsUnlocked);
        Assert.Equal(firstCheck.PrerequisiteKeys!.Length, lockedCheck.MissingPrerequisites.Length);
        foreach (var source in pack.Lessons)
        {
            Guid currentId = StarterLessonInstaller.ContentId(code + "/" + source.Key);
            await CompleteLessonAsync(browser, token, currentId, source);
        }
        Assert.Equal(0, await installer.InstallAsync(CancellationToken.None));
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(pack.Lessons.Length, await database.LessonProgress.CountAsync(item => item.UserId == account.Id && item.Status == "Completed"));
        var owner = await database.Users.SingleAsync(item => item.Id == StarterLessonInstaller.ContentId("system-owner"));
        Assert.Null(owner.PasswordHash);
        Assert.Equal("Disabled", owner.Status);
        Assert.False(await database.UserRoles.AnyAsync(item => item.UserId == owner.Id));
        Assert.Empty(await database.ContentReviews.Where(item => database.LessonVersions.Any(version => version.Id == item.LessonVersionId && version.CreatedBy == owner.Id)).ToArrayAsync());
    }

    private static async Task CompleteLessonAsync(SessionTestBrowser browser, string token, Guid lessonId, StarterLesson source)
    {
        var start = await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lessonId}/sessions", bearer: token);
        start.EnsureSuccessStatusCode();
        var session = (await start.Content.ReadFromJsonAsync<SessionView>())!;
        Assert.Equal(source.Exercises.Length, session.TotalCount);
        if (source.IsCheckpoint)
        {
            Assert.DoesNotContain(" — ", session.Explanation);
            Assert.True(session.TotalCount >= 12);
        }
        int index = 0;
        while (session.CurrentExercise is { } exercise)
        {
            var question = source.Exercises[index++];
            bool sentence = question.Type == "SentenceBuilder";
            Guid[] choices = sentence ? [] : exercise.Options.Where(item => item.Text == question.Answers[0]).Select(item => item.Id).ToArray();
            Guid[] tokens = sentence ? question.TokenOrders![0].Select(position =>
                exercise.Options.Single(item => item.Text == question.Options[position - 1]).Id).ToArray() : [];
            var response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
                new
                {
                    sessionExerciseId = exercise.Id,
                    answerText = choices.Length == 0 && !sentence ? question.Answers[0] : null,
                    selectedOptionIds = choices,
                    orderedTokenIds = tokens,
                    idempotencyKey = Guid.NewGuid()
                }, token);
            response.EnsureSuccessStatusCode();
            Assert.True((await response.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect);
            var reopen = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: token);
            reopen.EnsureSuccessStatusCode();
            session = (await reopen.Content.ReadFromJsonAsync<SessionView>())!;
            Assert.Equal(index, session.AnsweredCount);
            Assert.DoesNotContain("acceptedAnswers", await reopen.Content.ReadAsStringAsync());
        }
        var completion = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token);
        completion.EnsureSuccessStatusCode();
        var completed = (await completion.Content.ReadFromJsonAsync<SessionView>())!;
        Assert.True(completed.Passed);
        Assert.Equal(100, completed.Score);
        Assert.Equal(20, completed.AwardedXp);
    }
}
