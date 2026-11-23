using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class LearningTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private SessionTestBrowser Browser() => new(fixture.CreateIsolatedClient());
    private async Task<(SecurityTestAccount User, string Token, LearningTestContent Content)> SetupAsync(SessionTestBrowser browser, int count = 2, string type = "Translation", int courseNumber = 1)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        string token = await user.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id, count, type, courseNumber);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token)).EnsureSuccessStatusCode();
        return (user, token, content);
    }
    private static async Task<SessionView> ReadAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionView>())!;
    }
    private static Task<HttpResponseMessage> StartAsync(SessionTestBrowser browser, string token, Guid lessonId) => browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lessonId}/sessions", bearer: token);
    private static Task<HttpResponseMessage> AnswerAsync(SessionTestBrowser browser, string token, SessionView session, string answer, Guid? key = null) => browser.SendAsync(HttpMethod.Post,
        $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise!.Id, answerText = answer, selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = key ?? Guid.NewGuid() }, token);
    private static Task<HttpResponseMessage> CompleteAsync(SessionTestBrowser browser, string token, Guid id) => browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{id}/complete", bearer: token);
    private static Task<HttpResponseMessage> GetAsync(SessionTestBrowser browser, string token, Guid id) => browser.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{id}", bearer: token);

    [Theory]
    [InlineData(1, "café", "cafe", true)]
    [InlineData(2, "perché", "perche", true)]
    [InlineData(3, "für", "fur", true)]
    [InlineData(3, "für", "far", false)]
    [InlineData(4, "ёлка", "елка", false)]
    [InlineData(4, "café", "cafe", false)]
    public async Task TypedKeyboardFallbackRespectsTheCourseAndKeepsHeartsForAcceptedAnswers(
        int courseNumber, string standardSpelling, string answer, bool isCorrect)
    {
        using var browser = Browser();
        var (_, token, content) = await SetupAsync(browser, count: 1, courseNumber: courseNumber);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var accepted = await database.AcceptedAnswers.SingleAsync(item => item.ExerciseId == content.ExerciseIds[0]);
            accepted.AnswerText = standardSpelling;
            await database.SaveChangesAsync();
        }
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        var response = await AnswerAsync(browser, token, session, answer);
        response.EnsureSuccessStatusCode();
        var feedback = (await response.Content.ReadFromJsonAsync<AnswerFeedback>())!;
        feedback.IsCorrect.Should().Be(isCorrect);
        feedback.HeartsRemaining.Should().Be(isCorrect ? session.Hearts : session.Hearts - 1);
        if (isCorrect) feedback.Feedback.Should().Contain(standardSpelling);
    }

    [Fact]
    public async Task EnrollmentAndStartAreIdempotentAndPrivateAcrossCourses()
    {
        using var browser = Browser(); var (user, token, content) = await SetupAsync(browser);
        var results = await Task.WhenAll(StartAsync(browser, token, content.LessonId), StartAsync(browser, token, content.LessonId));
        using var created = Assert.Single(results, response => response.StatusCode == HttpStatusCode.Created);
        using var resumed = Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
        var first = await ReadAsync(created);
        var second = await ReadAsync(resumed);
        first.Id.Should().Be(second.Id);
        first.TotalCount.Should().Be(2); first.Hearts.Should().Be(5);
        Assert.NotNull(created.Headers.Location);
        Assert.Equal($"/api/v1/me/learning-sessions/{first.Id}", created.Headers.Location.AbsolutePath);
        using var detail = await browser.SendAsync(HttpMethod.Get, created.Headers.Location.AbsolutePath, bearer: token);
        detail.EnsureSuccessStatusCode();
        string json = await detail.Content.ReadAsStringAsync();
        json.Should().NotContain("privateEvaluation").And.NotContain("correctOption").And.NotContain("Hola is a greeting").And.NotContain("acceptedAnswers");
        using var other = Browser(); var outsider = await SecurityTestAccount.CreateAsync(fixture); string otherToken = await outsider.LoginAsync(other);
        (await GetAsync(other, otherToken, first.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CompleteAsync(other, otherToken, first.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AnswerAsync(other, otherToken, first, "Hola")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await fixture.Client.GetAsync($"/api/v1/me/learning-sessions/{first.Id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await StartAsync(other, otherToken, content.LessonId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await Task.WhenAll(browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token), browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token));
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.Enrollments.CountAsync(item => item.UserId == user.Id)).Should().Be(1);
        var another = await LearningTestContent.CreateAsync(fixture, user.Id, courseNumber: 2);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { another.CourseId }, token)).EnsureSuccessStatusCode();
        (await db.Enrollments.CountAsync(item => item.UserId == user.Id)).Should().Be(2);
        (await db.LearningSessions.CountAsync(item => item.UserId == user.Id && item.CourseId == content.CourseId)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentAnswersAndCompletionAwardOnceAndRepeatAwardsNothing()
    {
        using var browser = Browser(); var (user, token, content) = await SetupAsync(browser);
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        (await CompleteAsync(browser, token, session.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        Guid key = Guid.NewGuid(); var answers = await Task.WhenAll(AnswerAsync(browser, token, session, " hola ", key), AnswerAsync(browser, token, session, " hola ", key));
        foreach (var answer in answers) answer.EnsureSuccessStatusCode();
        (await answers[0].Content.ReadAsStringAsync()).Should().Be(await answers[1].Content.ReadAsStringAsync());
        (await AnswerAsync(browser, token, session, "Adiós", key)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        session = await ReadAsync(await GetAsync(browser, token, session.Id)); session.AnsweredCount.Should().Be(1);
        (await AnswerAsync(browser, token, session, "Hola")).EnsureSuccessStatusCode();
        var completed = await Task.WhenAll(CompleteAsync(browser, token, session.Id), CompleteAsync(browser, token, session.Id));
        foreach (var response in completed) (await ReadAsync(response)).AwardedXp.Should().Be(20);
        var result = await ReadAsync(await GetAsync(browser, token, session.Id));
        result.Score.Should().Be(100); result.CurrentExercise.Should().BeNull(); result.Results.Should().HaveCount(2);
        var repeated = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        while (repeated.CurrentExercise is not null)
        {
            (await AnswerAsync(browser, token, repeated, "Hola")).EnsureSuccessStatusCode();
            repeated = await ReadAsync(await GetAsync(browser, token, repeated.Id));
        }
        (await ReadAsync(await CompleteAsync(browser, token, repeated.Id))).AwardedXp.Should().Be(0);
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.XpTransactions.Where(item => item.UserId == user.Id).SumAsync(item => item.Amount)).Should().Be(20);
        (await db.LessonProgress.SingleAsync(item => item.UserId == user.Id)).Status.Should().Be("Completed");
        var daily = await db.DailyActivities.SingleAsync(item => item.UserId == user.Id);
        daily.EarnedXp.Should().Be(20); daily.CompletedLessons.Should().Be(1); daily.GoalReached.Should().BeTrue();
        string localDate = daily.LocalDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        using var activity = await browser.SendAsync(HttpMethod.Get,
            $"/api/v1/me/activity?from={localDate}&to={localDate}", bearer: token);
        activity.EnsureSuccessStatusCode();
        var days = (await activity.Content.ReadFromJsonAsync<ActivityPage>())!;
        days.Items.Should().ContainSingle().Which.Should().Be(new ActivityDay(daily.LocalDate, 20, 1, true));
    }

    [Fact]
    public async Task ExhaustedHeartsBlockAnswersButFreePracticeWorksWithoutRewards()
    {
        using var browser = Browser(); var (user, token, content) = await SetupAsync(browser, 6);
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        for (int index = 0; index < 5; index++)
        {
            (await AnswerAsync(browser, token, session, "wrong")).EnsureSuccessStatusCode();
            session = await ReadAsync(await GetAsync(browser, token, session.Id));
        }
        session.Hearts.Should().Be(0); session.CurrentExercise.Should().BeNull(); session.AnsweredCount.Should().Be(5);
        (await ReadAsync(await CompleteAsync(browser, token, session.Id))).AwardedXp.Should().Be(0);
        var practice = await ReadAsync(await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", new { content.LessonId }, token));
        while (practice.CurrentExercise is not null)
        {
            (await AnswerAsync(browser, token, practice, "wrong")).EnsureSuccessStatusCode();
            practice = await ReadAsync(await GetAsync(browser, token, practice.Id));
        }
        practice.Hearts.Should().Be(5);
        var result = await ReadAsync(await CompleteAsync(browser, token, practice.Id)); result.Mode.Should().Be("Practice"); result.AwardedXp.Should().Be(0);
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.XpTransactions.AnyAsync(item => item.UserId == user.Id)).Should().BeFalse();
        (await db.LessonProgress.SingleAsync(item => item.UserId == user.Id)).Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task SessionKeepsSnapshotsAfterSourceChangesAndCourseArchive()
    {
        using var browser = Browser(); var (_, token, content) = await SetupAsync(browser, 1);
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await db.AcceptedAnswers.Where(item => content.ExerciseIds.Contains(item.ExerciseId)).ExecuteUpdateAsync(update => update.SetProperty(item => item.AnswerText, "Changed answer"));
            await db.LessonVersions.Where(item => item.Id == content.VersionId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Title, "Changed title"));
            await db.Lessons.Where(item => item.Id == content.LessonId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Archived"));
            await db.Courses.Where(item => item.Id == content.CourseId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsPublished, false));
        }
        session = await ReadAsync(await GetAsync(browser, token, session.Id)); session.Title.Should().Be("Hello");
        var feedback = await (await AnswerAsync(browser, token, session, "Hola")).Content.ReadFromJsonAsync<AnswerFeedback>(); feedback!.IsCorrect.Should().BeTrue();
        (await ReadAsync(await CompleteAsync(browser, token, session.Id))).AwardedXp.Should().Be(20);
        (await StartAsync(browser, token, content.LessonId)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChoiceIdsAndOrderAreValidatedBeforeAnAttemptIsSaved()
    {
        using var browser = Browser(); var (user, token, content) = await SetupAsync(browser, 2, "MultipleChoice");
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        Guid correct = session.CurrentExercise!.Options.Single(item => item.Text == "Hola").Id;
        async Task<HttpResponseMessage> Submit(Guid exercise, Guid option, Guid? key = null) => await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new { sessionExerciseId = exercise, selectedOptionIds = new[] { option }, idempotencyKey = key ?? Guid.NewGuid() }, token);
        (await Submit(Guid.NewGuid(), correct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Submit(session.CurrentExercise.Id, Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.Conflict);
        Guid key = Guid.NewGuid(); (await Submit(session.CurrentExercise.Id, correct, key)).EnsureSuccessStatusCode();
        (await Submit(session.CurrentExercise.Id, correct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var after = await ReadAsync(await GetAsync(browser, token, session.Id)); after.AnsweredCount.Should().Be(1); after.Hearts.Should().Be(5);
        (await Submit(after.CurrentExercise!.Id, after.CurrentExercise.Options[0].Id, key)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        // Overposted scoring and null collections never reach the service.
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = after.CurrentExercise.Id, answerText = "Hola", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid(), awardedXp = 999 }, token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.AnswerAttempts.CountAsync(item => item.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task PrerequisitesUnlockOnlyAfterPassingAndCompletedBasicsRemainOpen()
    {
        using var browser = Browser(); var (user, token, content) = await SetupAsync(browser, 1);
        Guid dependent;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var original = await db.Lessons.SingleAsync(item => item.Id == content.LessonId);
            var lesson = new Lesson { UnitId = original.UnitId, OwnerId = user.Id, Title = "Next", Objective = "Next", OrderNumber = 2, Status = "Published" };
            var version = new LessonVersion { LessonId = lesson.Id, CreatedBy = user.Id, Title = "Next", Objective = "Next", Explanation = "English notes", VersionNumber = 1, Status = "Published" };
            var exercise = new Exercise { LessonVersionId = version.Id, Type = "Translation", Prompt = "Hello", Explanation = "Greeting", OrderNumber = 1 };
            db.AddRange(lesson, version, exercise, new AcceptedAnswer { ExerciseId = exercise.Id, AnswerText = "Hola" });
            await db.SaveChangesAsync(); lesson.CurrentVersionId = version.Id;
            db.LessonPrerequisites.Add(new LessonPrerequisite { LessonId = lesson.Id, PrerequisiteLessonId = content.LessonId });
            await db.SaveChangesAsync(); dependent = lesson.Id;
        }
        (await StartAsync(browser, token, dependent)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var before = (await (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/progress?courseId={content.CourseId}", bearer: token)).Content.ReadFromJsonAsync<CourseProgress>())!;
        before.Lessons.Single(item => item.Id == dependent).IsUnlocked.Should().BeFalse();
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        (await AnswerAsync(browser, token, session, "Hola")).EnsureSuccessStatusCode();
        (await CompleteAsync(browser, token, session.Id)).EnsureSuccessStatusCode();
        (await StartAsync(browser, token, dependent)).EnsureSuccessStatusCode();
        (await StartAsync(browser, token, content.LessonId)).EnsureSuccessStatusCode();
    }


    [Theory]
    [InlineData(1, 80, 20, "Completed")]
    [InlineData(2, 60, 0, "InProgress")]
    public async Task PassingThresholdAndLocalDailyActivityAreAppliedOnServer(int wrong, int expectedScore, int expectedXp, string expectedStatus)
    {
        using var browser = Browser(); var (user, token, content) = await SetupAsync(browser, 5);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await db.UserPreferences.Where(item => item.UserId == user.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.TimeZone, "Pacific/Kiritimati"));
        }
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        for (int index = 0; index < 5; index++)
        {
            (await AnswerAsync(browser, token, session, index < wrong ? "wrong" : "Hola")).EnsureSuccessStatusCode();
            session = await ReadAsync(await GetAsync(browser, token, session.Id));
        }
        var result = await ReadAsync(await CompleteAsync(browser, token, session.Id));
        result.Score.Should().Be(expectedScore); result.AwardedXp.Should().Be(expectedXp);
        result.Passed.Should().Be(expectedStatus == "Completed");
        await using var check = fixture.Services.CreateAsyncScope(); var database = check.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.LessonProgress.SingleAsync(item => item.UserId == user.Id)).Status.Should().Be(expectedStatus);
        var activity = await database.DailyActivities.SingleOrDefaultAsync(item => item.UserId == user.Id);
        if (expectedXp > 0)
        {
            activity.Should().NotBeNull();
            activity!.LocalDate.Should().Be(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(result.CompletedAt!.Value, TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati")).DateTime));
        }
        else activity.Should().BeNull();
    }

    [Fact]
    public async Task EnrollmentListingIsPagedOwnedAndRetainsAnArchivedCourse()
    {
        using var browser = Browser();
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        string token = await learner.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, learner.Id, 1);
        const string enrollments = "/api/v1/me/enrollments";
        var input = new { content.CourseId, startingLevel = "A2" };
        var responses = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Post, enrollments, input, token),
            browser.SendAsync(HttpMethod.Post, enrollments, input, token));
        using var created = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        using var replay = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var receipt = await created.Content.ReadFromJsonAsync<EnrollmentReceipt>();
        Assert.NotNull(receipt);
        Assert.Equal(receipt, await replay.Content.ReadFromJsonAsync<EnrollmentReceipt>());
        Assert.NotNull(created.Headers.Location);
        string detailPath = created.Headers.Location.AbsolutePath;
        Assert.Equal($"/api/v1/me/enrollments/{content.CourseId}", detailPath);
        using var noLevel = await browser.SendAsync(HttpMethod.Post, enrollments, new { content.CourseId }, token);
        Assert.Equal(HttpStatusCode.OK, noLevel.StatusCode);
        using var detail = await browser.SendAsync(HttpMethod.Get, detailPath, bearer: token);
        detail.EnsureSuccessStatusCode();
        var entry = await detail.Content.ReadFromJsonAsync<EnrollmentView>();
        Assert.NotNull(entry);
        Assert.Equal(receipt.EnrolledAt, entry.EnrolledAt);
        Assert.Equal("Self:A2", entry.PlacementLevel);
        var listResponse = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/enrollments", bearer: token);
        listResponse.EnsureSuccessStatusCode();
        var list = (await listResponse.Content.ReadFromJsonAsync<EnrollmentPage>())!;
        list.TotalCount.Should().Be(1); list.Items.Should().ContainSingle(item => item.CourseId == content.CourseId);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/enrollments?page=0", bearer: token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = Guid.NewGuid() }, token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var outsider = Browser(); var otherUser = await SecurityTestAccount.CreateAsync(fixture); string otherToken = await otherUser.LoginAsync(outsider);
        using var hidden = await outsider.SendAsync(HttpMethod.Get, detailPath, bearer: otherToken);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var other = (await (await outsider.SendAsync(HttpMethod.Get, "/api/v1/me/enrollments", bearer: otherToken)).Content.ReadFromJsonAsync<EnrollmentPage>())!;
        other.TotalCount.Should().Be(0);
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await db.Courses.Where(item => item.Id == content.CourseId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsPublished, false));
        }
        var progress = (await (await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/progress?courseId={content.CourseId}", bearer: token)).Content.ReadFromJsonAsync<CourseProgress>())!;
        progress.OpenSessions.Should().ContainSingle(item => item.Id == session.Id);
        using var archived = await browser.SendAsync(HttpMethod.Get, detailPath, bearer: token);
        archived.EnsureSuccessStatusCode();
        Assert.False((await archived.Content.ReadFromJsonAsync<EnrollmentView>())!.IsPublished);
        (await outsider.SendAsync(HttpMethod.Get, $"/api/v1/me/progress?courseId={content.CourseId}", bearer: otherToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    [Theory]
    [InlineData(false, false, "hola", false)]
    [InlineData(true, false, "hola!", false)]
    [InlineData(true, true, "hola!", true)]
    public async Task PerExerciseCaseAndPunctuationRulesAreSnapshotted(bool ignoreCase, bool ignoreTerminalPunctuation, string answer, bool expected)
    {
        using var browser = Browser(); var (_, token, content) = await SetupAsync(browser, 1);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            string rules = LanguageLearning.Infrastructure.Content.TextEvaluationRulesCodec.Write(new TextEvaluationRules(ignoreCase, ignoreTerminalPunctuation));
            await db.Exercises.Where(item => item.Id == content.ExerciseIds[0]).ExecuteUpdateAsync(update => update.SetProperty(item => item.EvaluationRules, rules));
        }
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        var feedback = (await (await AnswerAsync(browser, token, session, answer)).Content.ReadFromJsonAsync<AnswerFeedback>())!;
        feedback.IsCorrect.Should().Be(expected);
    }

    [Theory]
    [InlineData("Dov’è la stazione?", "Dov'è la stazione?", true)]
    [InlineData("¿Qué hora es?", "Qué hora es", true)]
    [InlineData("¿Qué hora es?", "Que hora es", true)]
    [InlineData("año", "ano", false)]
    [InlineData("l’acqua", "lacqua", false)]
    public async Task KeyboardVariantsAreGradedOnTheServerWithoutDiscardingMeaningfulLetters(string accepted, string typed, bool expected)
    {
        using var browser = Browser();
        var (_, token, content) = await SetupAsync(browser, 1);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await db.AcceptedAnswers.Where(item => item.ExerciseId == content.ExerciseIds[0])
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.AnswerText, accepted));
            string rules = LanguageLearning.Infrastructure.Content.TextEvaluationRulesCodec.Write(new TextEvaluationRules(true, true));
            await db.Exercises.Where(item => item.Id == content.ExerciseIds[0])
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.EvaluationRules, rules));
        }
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        var feedback = (await (await AnswerAsync(browser, token, session, typed)).Content.ReadFromJsonAsync<AnswerFeedback>())!;
        feedback.IsCorrect.Should().Be(expected);
        feedback.HeartsRemaining.Should().Be(expected ? 5 : 4);
        var reopened = await ReadAsync(await GetAsync(browser, token, session.Id));
        reopened.LastFeedback!.Feedback.Should().Be(feedback);
    }


    [Fact]
    public async Task HighScoreWithUnansweredQuestionsStillFailsAfterHeartsRunOut()
    {
        using var browser = Browser(); var (_, token, content) = await SetupAsync(browser, 30);
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        for (int index = 0; index < 29; index++)
        {
            (await AnswerAsync(browser, token, session, index < 24 ? "Hola" : "wrong")).EnsureSuccessStatusCode();
            session = await ReadAsync(await GetAsync(browser, token, session.Id));
        }
        session.Hearts.Should().Be(0);
        var result = await ReadAsync(await CompleteAsync(browser, token, session.Id));
        result.Score.Should().Be(80); result.Passed.Should().BeFalse(); result.AwardedXp.Should().Be(0);
        result.Accuracy.Should().Be(82);
    }

    [Fact]
    public async Task PostgreSqlRejectsCrossOwnerAttemptsAndDuplicateRewardSources()
    {
        using var browser = Browser(); var (user, token, content) = await SetupAsync(browser, 1);
        var session = await ReadAsync(await StartAsync(browser, token, content.LessonId));
        var other = await SecurityTestAccount.CreateAsync(fixture);
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AnswerAttempts\" (\"Id\",\"UserId\",\"SessionExerciseId\",\"AnswerPayload\",\"ResponseSnapshot\",\"IsCorrect\",\"IdempotencyKey\",\"SubmittedAt\") VALUES ({Guid.NewGuid()},{other.Id},{session.CurrentExercise!.Id},'{{}}'::jsonb,'{{}}'::jsonb,false,{Guid.NewGuid()},{DateTimeOffset.UtcNow})"))
            .Should().ThrowAsync<PostgresException>().Where(error => error.SqlState == PostgresErrorCodes.ForeignKeyViolation);
        (await AnswerAsync(browser, token, session, "Hola")).EnsureSuccessStatusCode();
        (await CompleteAsync(browser, token, session.Id)).EnsureSuccessStatusCode();
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"XpTransactions\" (\"Id\",\"UserId\",\"CourseId\",\"SourceType\",\"SourceId\",\"Amount\",\"EarnedAt\") VALUES ({Guid.NewGuid()},{user.Id},{content.CourseId},'FirstLessonCompletion',{content.LessonId},20,{DateTimeOffset.UtcNow})"))
            .Should().ThrowAsync<PostgresException>().Where(error => error.SqlState == PostgresErrorCodes.UniqueViolation);
    }
}
