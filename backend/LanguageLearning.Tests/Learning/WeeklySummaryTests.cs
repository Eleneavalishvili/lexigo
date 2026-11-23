using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class WeeklySummaryTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task EmptySummaryIsPrivateBoundedAndDoesNotInventAccuracyOrStudy()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        const string path = "/api/v1/me/weekly-summary";
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, path)).StatusCode);
        var user = await SecurityTestAccount.CreateAsync(fixture);
        string token = await user.LoginAsync(browser);
        var response = await browser.SendAsync(HttpMethod.Get, path, bearer: token);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var summary = (await response.Content.ReadFromJsonAsync<WeeklySummaryView>())!;
        Assert.Equal(7, summary.Days.Length);
        Assert.Equal(DayOfWeek.Monday, summary.WeekStart.DayOfWeek);
        Assert.Equal(0, summary.EarnedXp);
        Assert.Equal(0, summary.StudyDays);
        Assert.Null(summary.Accuracy);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, path + "?weeksAgo=-1", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, path + "?weeksAgo=13", bearer: token)).StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.RefreshSessions.Where(item => item.UserId == user.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, path, bearer: token)).StatusCode);
    }

    [Fact]
    public async Task SummaryScopesCoursesCountsReviewedWordsOnceAndExcludesFutureAndUngradedAccuracy()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var other = await SecurityTestAccount.CreateAsync(fixture);
        var spanish = await LearningTestContent.CreateAsync(fixture, user.Id, count: 4);
        var italian = await LearningTestContent.CreateAsync(fixture, user.Id, courseNumber: 2);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var now = DateTimeOffset.UtcNow;
        var monday = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-((int)now.DayOfWeek + 6) % 7);
        var start = new DateTimeOffset(monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        db.Enrollments.AddRange(new() { UserId = user.Id, CourseId = spanish.CourseId },
            new() { UserId = user.Id, CourseId = italian.CourseId }, new() { UserId = other.Id, CourseId = spanish.CourseId });
        db.XpTransactions.AddRange(
            Xp(user.Id, spanish.CourseId, 20, now), Xp(user.Id, italian.CourseId, 40, now),
            Xp(other.Id, spanish.CourseId, 500, now), Xp(user.Id, spanish.CourseId, 1000, now.AddHours(2)),
            Xp(user.Id, spanish.CourseId, 300, start.AddSeconds(-1)));
        db.LessonProgress.Add(new() { UserId = user.Id, LessonId = spanish.LessonId, Status = "Completed", BestScore = 100, CompletedAt = now });
        var word = new VocabularyItem { CourseId = spanish.CourseId, Word = "summary-" + Guid.NewGuid().ToString("N"), EnglishTranslation = "fixture" };
        var saved = new SavedWord { UserId = user.Id, VocabularyItemId = word.Id, DueAt = now.AddHours(-1) };
        db.VocabularyItems.Add(word);
        db.SavedWords.Add(saved);
        db.WordReviews.AddRange(
            new() { SavedWordId = saved.Id, IsCorrect = true, ReviewedAt = now, IdempotencyKey = Guid.NewGuid(), DueAt = now.AddDays(1), IntervalDays = 1 },
            new() { SavedWordId = saved.Id, IsCorrect = false, ReviewedAt = now, IdempotencyKey = Guid.NewGuid(), DueAt = now.AddDays(1), IntervalDays = 1 });
        var session = new LearningSession
        {
            UserId = user.Id,
            CourseId = spanish.CourseId,
            LessonId = spanish.LessonId,
            LessonVersionId = spanish.VersionId,
            Title = "Summary practice fixture",
            Explanation = "",
            Mode = "Practice",
            FirstCompletionXp = 0,
            StartedAt = now
        };
        db.LearningSessions.Add(session);
        for (int index = 0; index < 4; index++)
        {
            var exercise = new SessionExercise
            {
                UserId = user.Id,
                SessionId = session.Id,
                ExerciseId = spanish.ExerciseIds[index],
                OrderNumber = index + 1,
                PublicSnapshot = "{}",
                PrivateEvaluationSnapshot = "{}"
            };
            db.SessionExercises.Add(exercise);
            db.AnswerAttempts.Add(new()
            {
                UserId = user.Id,
                SessionExerciseId = exercise.Id,
                AnswerPayload = "{}",
                ResponseSnapshot = "{}",
                IsGraded = index != 2,
                IsCorrect = index == 0,
                IdempotencyKey = Guid.NewGuid(),
                SubmittedAt = index == 3 ? now.AddHours(2) : now
            });
        }
        var cafe = new Dialogue { CourseId = spanish.CourseId, Scenario = "cafe", Title = "Café" };
        var hotel = new Dialogue { CourseId = spanish.CourseId, Scenario = "hotel", Title = "Hotel" };
        var cafeEnd = new DialogueNode { DialogueId = cafe.Id, Text = "Goodbye", IsTerminal = true };
        var hotelEnd = new DialogueNode { DialogueId = hotel.Id, Text = "Goodbye", IsTerminal = true };
        db.AddRange(cafe, hotel, cafeEnd, hotelEnd);
        db.DialogueSessions.AddRange(
            Finished(cafe, cafeEnd, user.Id, now), Finished(cafe, cafeEnd, user.Id, now),
            Finished(hotel, hotelEnd, other.Id, now), Finished(hotel, hotelEnd, user.Id, now.AddHours(2)));
        await db.SaveChangesAsync();
        var summary = (await new WeeklySummaryQueries(db, new Clock(now)).GetAsync(user.Id, 0, default))!;
        Assert.Equal(1, summary.ConversationsFinished);
        Assert.Equal(1, summary.Courses.Single(item => item.CourseId == spanish.CourseId).ConversationsFinished);
        Assert.Equal(0, summary.Courses.Single(item => item.CourseId == italian.CourseId).ConversationsFinished);

        static DialogueSession Finished(Dialogue dialogue, DialogueNode ending, Guid learnerId, DateTimeOffset at) => new()
        {
            DialogueId = dialogue.Id, CurrentNodeId = ending.Id, UserId = learnerId,
            StartedAt = at.AddMinutes(-1), CompletedAt = at, VersionSnapshot = "{}"
        };
        Assert.Equal(60, summary.EarnedXp);
        Assert.Equal(1, summary.NewLessonsCompleted);
        Assert.Equal(1, summary.ReviewedWords);
        Assert.Equal(2, summary.GradedAnswers);
        Assert.Equal(50, summary.Accuracy);
        Assert.Equal(1, summary.StudyDays);
        Assert.Equal(60, summary.Days.Sum(item => item.EarnedXp));
        Assert.Equal(20, Assert.Single(summary.Courses, item => item.CourseId == spanish.CourseId).EarnedXp);
        Assert.Equal(1, Assert.Single(summary.Courses, item => item.CourseId == spanish.CourseId).DueWords);
        Assert.Null(Assert.Single(summary.Courses, item => item.CourseId == italian.CourseId).Accuracy);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await user.LoginAsync(browser);
        var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/weekly-summary?userId=" + other.Id, bearer: token);
        response.EnsureSuccessStatusCode();
        Assert.Equal(60, (await response.Content.ReadFromJsonAsync<WeeklySummaryView>())!.EarnedXp);
    }

    [Fact]
    public async Task SavedTimeZoneKeepsTheLastDayOfADaylightSavingWeekAndExcludesItsNeighbors()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.UserPreferences.SingleAsync(item => item.UserId == user.Id)).TimeZone = "Europe/Berlin";
        db.Enrollments.Add(new() { UserId = user.Id, CourseId = content.CourseId });
        db.XpTransactions.AddRange(
            Xp(user.Id, content.CourseId, 10, new(2026, 10, 18, 22, 0, 0, TimeSpan.Zero)),
            Xp(user.Id, content.CourseId, 20, new(2026, 10, 25, 22, 30, 0, TimeSpan.Zero)),
            Xp(user.Id, content.CourseId, 500, new(2026, 10, 18, 21, 59, 0, TimeSpan.Zero)),
            Xp(user.Id, content.CourseId, 500, new(2026, 10, 25, 23, 0, 0, TimeSpan.Zero)));
        await db.SaveChangesAsync();
        var query = new WeeklySummaryQueries(db, new Clock(new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero)));
        var summary = (await query.GetAsync(user.Id, 1, default))!;
        Assert.Equal(new DateOnly(2026, 10, 19), summary.WeekStart);
        Assert.Equal(30, summary.EarnedXp);
        Assert.Equal(2, summary.StudyDays);
        Assert.Equal(20, summary.Days[^1].EarnedXp);
        Assert.Null(await query.GetAsync(Guid.NewGuid(), 0, default));
    }

    [Fact]
    public async Task UngradedPracticeCountsAsActivityWithoutXpAndPlacementIsExcluded()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.Enrollments.Add(new() { UserId = user.Id, CourseId = content.CourseId });
        void AddAttempt(bool placement, int question)
        {
            var session = new LearningSession
            {
                UserId = user.Id,
                CourseId = content.CourseId,
                LessonId = placement ? null : content.LessonId,
                LessonVersionId = content.VersionId,
                Title = "Summary activity fixture",
                Explanation = "",
                Mode = "Practice",
                FirstCompletionXp = 0,
                PlacementMap = placement ? "{}" : null,
                StartedAt = now
            };
            var exercise = new SessionExercise
            {
                UserId = user.Id,
                SessionId = session.Id,
                ExerciseId = content.ExerciseIds[question],
                OrderNumber = 1,
                PublicSnapshot = "{}",
                PrivateEvaluationSnapshot = "{}"
            };
            db.LearningSessions.Add(session);
            db.SessionExercises.Add(exercise);
            db.AnswerAttempts.Add(new()
            {
                UserId = user.Id,
                SessionExerciseId = exercise.Id,
                IsGraded = placement,
                IsCorrect = placement,
                SubmittedAt = now,
                IdempotencyKey = Guid.NewGuid(),
                AnswerPayload = "{}",
                ResponseSnapshot = "{}"
            });
        }
        AddAttempt(true, 0);
        await db.SaveChangesAsync();
        var query = new WeeklySummaryQueries(db, new Clock(now));
        Assert.Equal(0, (await query.GetAsync(user.Id, 0, default))!.StudyDays);
        AddAttempt(false, 1);
        await db.SaveChangesAsync();
        var summary = (await query.GetAsync(user.Id, 0, default))!;
        Assert.Equal(1, summary.StudyDays);
        Assert.Equal(0, summary.EarnedXp);
        Assert.Null(summary.Accuracy);
        Assert.Equal(0, summary.GradedAnswers);
    }

    private static XpTransaction Xp(Guid user, Guid course, int amount, DateTimeOffset at) => new()
    {
        UserId = user,
        CourseId = course,
        Amount = amount,
        EarnedAt = at,
        SourceType = "WeeklySummaryFixture",
        SourceId = Guid.NewGuid()
    };
}
