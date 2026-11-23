using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class GuestTrialTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task FourPublishedTrialsTeachFirstAndGradeWithoutCreatingLearningOrRewards()
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>().InstallAsync(default);
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        int usersBefore = await db.Users.CountAsync();
        foreach (string language in new[] { "es", "it", "de", "ru" })
        {
            Guid course = StarterLessonInstaller.Read(language).CourseId;
            var response = await fixture.Client.GetAsync($"/api/v1/courses/{course}/trial");
            response.EnsureSuccessStatusCode();
            Assert.True(response.Headers.CacheControl?.NoStore);
            string json = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("correctAnswer", json);
            Assert.DoesNotContain("evaluationRules", json);
            Assert.DoesNotContain("answerText", json);
            var trial = (await response.Content.ReadFromJsonAsync<GuestTrial>())!;
            Assert.Contains(" — ", trial.Explanation);
            Assert.Equal(3, trial.Questions.Length);
            var question = trial.Questions[0];
            string accepted = await db.AcceptedAnswers.Where(item => item.ExerciseId == question.Id)
                .Select(item => item.AnswerText).FirstAsync();
            foreach (bool correct in new[] { false, true })
            {
                var option = question.Options.First(item => (item.Text == accepted) == correct);
                var answer = await fixture.Client.PostAsJsonAsync($"/api/v1/courses/{course}/trial/answers",
                    new TrialAnswer(trial.VersionId, question.Id, option.Id));
                answer.EnsureSuccessStatusCode();
                var feedback = (await answer.Content.ReadFromJsonAsync<TrialFeedback>())!;
                Assert.Equal(correct, feedback.IsCorrect);
                Assert.Equal(accepted, feedback.CorrectAnswer);
                Assert.False(string.IsNullOrWhiteSpace(feedback.Explanation));
            }
        }
        Assert.Equal(usersBefore, await db.Users.CountAsync());
        Assert.False(await db.Enrollments.AnyAsync());
        Assert.False(await db.LearningSessions.AnyAsync());
        Assert.False(await db.XpTransactions.AnyAsync());
        Assert.False(await db.DailyActivities.AnyAsync());
        Assert.False(await db.LearnerHearts.AnyAsync());
    }

    [Fact]
    public async Task HiddenCoursesForeignOptionsAndChangedVersionsAreNotAvailableToTrials()
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>().InstallAsync(default);
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid course = StarterLessonInstaller.Read("es").CourseId;
        string path = $"/api/v1/courses/{course}/trial";
        var trial = (await fixture.Client.GetFromJsonAsync<GuestTrial>(path))!;
        var first = trial.Questions[0];
        var foreign = new TrialAnswer(trial.VersionId, first.Id, trial.Questions[1].Options[0].Id);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync(path + "/answers", foreign)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync(path + "/answers",
            new TrialAnswer(Guid.NewGuid(), first.Id, first.Options[0].Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync(path + "/answers",
            new TrialAnswer(Guid.Empty, first.Id, first.Options[0].Id))).StatusCode);
        await db.Courses.Where(item => item.Id == course).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsPublished, false));
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync(path + "/answers",
            new TrialAnswer(trial.VersionId, first.Id, first.Options[0].Id))).StatusCode);
        await db.Courses.Where(item => item.Id == course).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsPublished, true));
    }
}
