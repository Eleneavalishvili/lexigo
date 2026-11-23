using System.Net.Http.Json;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Vocabulary;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Vocabulary;

public sealed class WordReviewArticleTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Theory]
    [InlineData("noun", "coffee", "the coffee", true)]
    [InlineData("noun", "coffee", "A coffee", true)]
    [InlineData("noun", "apple", "an apple", true)]
    [InlineData("noun", "coffee", "my coffee", false)]
    [InlineData("noun", "coffee", "the bad coffee", false)]
    [InlineData("adjective", "blue", "the blue", false)]
    [InlineData("numeral", "three", "the three", false)]
    public async Task OptionalArticlesApplyOnlyToNounMeaningReviews(string partOfSpeech, string english, string answer, bool correct)
    {
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid courseId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        (await database.Courses.SingleAsync(item => item.Id == courseId)).IsPublished = true;
        var word = new VocabularyItem
        {
            CourseId = courseId, Word = "test-" + Guid.NewGuid(), EnglishTranslation = english,
            Example = "An example for a word review.", PartOfSpeech = partOfSpeech
        };
        database.VocabularyItems.Add(word);
        await database.SaveChangesAsync();
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await learner.LoginAsync(browser);
        var save = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = word.Id }, token);
        save.EnsureSuccessStatusCode();
        var saved = (await save.Content.ReadFromJsonAsync<SaveWordResult>())!;
        var response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/saved-words/{saved.Id}/reviews",
            new { answer, idempotencyKey = Guid.NewGuid() }, token);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<WordReviewResult>())!;
        Assert.Equal(correct, result.IsCorrect);
        Assert.Equal(correct ? 1 : 0, result.IntervalDays);
        Assert.False(await database.XpTransactions.AnyAsync(item => item.UserId == learner.Id));
    }
}
