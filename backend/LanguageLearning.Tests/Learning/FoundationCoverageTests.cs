using System.Net.Http.Json;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed partial class StarterLessonTests
{
    [Theory]
    [InlineData("es")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("ru")]
    public async Task GrammarFoundationsCanBeLearnedAndCheckedWithRealGrading(string code)
    {
        await CompleteFoundationUnitAsync(code, "greetings", "grammar-foundations");
    }

    [Fact]
    public async Task AllThirtyThreeWrittenRussianLettersHaveGradedRecognition()
    {
        var pack = StarterLessonInstaller.Read("ru");
        var lessons = pack.Lessons.Where(lesson => lesson.Key.StartsWith("cyrillic-written-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, lessons.Length);
        var letters = lessons.SelectMany(lesson => lesson.Exercises).SelectMany(question => question.Answers).Distinct().ToArray();
        Assert.Equal(33, letters.Length);
        Assert.Contains("ё", letters);
        Assert.Contains("е", letters);
        Assert.Contains("ъ", letters);
        Assert.Contains("ь", letters);
        await CompleteFoundationUnitAsync("ru", "cyrillic-first-letters", "cyrillic-basics", "cyrillic-first-letters");
    }

    private async Task CompleteFoundationUnitAsync(string code, string prerequisite, string unitKey, string? exclude = null)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>().InstallAsync(default);
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        database.LessonProgress.Add(new LessonProgress
        {
            UserId = learner.Id,
            LessonId = StarterLessonInstaller.ContentId(code + "/" + prerequisite),
            Status = "Completed",
            BestScore = 100,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync();
        var pack = StarterLessonInstaller.Read(code);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await learner.LoginAsync(browser);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { pack.CourseId }, token)).EnsureSuccessStatusCode();
        foreach (var lesson in pack.Lessons.Where(lesson => lesson.UnitKey == unitKey && lesson.Key != exclude))
            await CompleteLessonAsync(browser, token, StarterLessonInstaller.ContentId(code + "/" + lesson.Key), lesson);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>().InstallAsync(default));
    }
}
