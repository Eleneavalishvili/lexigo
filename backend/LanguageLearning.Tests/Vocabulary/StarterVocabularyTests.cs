using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Vocabulary;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Vocabulary;

public sealed class StarterVocabularyTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task ConcurrentInstallationAddsWordsOnceAndRetainsEditedWordsAndSavedSchedules()
    {
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var database = firstScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var existing = await database.VocabularyItems.SingleAsync(item => item.Word == "hola");
        existing.Word = "HOLA";
        existing.Example = "Staff-edited example retained.";
        var saved = new SavedWord
        {
            UserId = learner.Id,
            VocabularyItemId = existing.Id,
            DueAt = DateTimeOffset.UtcNow.AddDays(7),
            IntervalDays = 7,
            CorrectStreak = 3
        };
        database.SavedWords.Add(saved);
        await database.SaveChangesAsync();
        var savedBefore = await database.SavedWords.AsNoTracking().SingleAsync(item => item.Id == saved.Id);
        var results = await Task.WhenAll(
            firstScope.ServiceProvider.GetRequiredService<StarterVocabularyInstaller>().InstallAsync(default),
            secondScope.ServiceProvider.GetRequiredService<StarterVocabularyInstaller>().InstallAsync(default));
        Assert.Equal(112, results.Sum());
        Assert.Contains(0, results);
        Assert.Equal(136, await database.VocabularyItems.CountAsync());
        Assert.Equal("Staff-edited example retained.", (await database.VocabularyItems.AsNoTracking()
            .SingleAsync(item => item.Id == existing.Id)).Example);
        foreach (var pack in StarterVocabularyInstaller.Read())
        {
            Guid courseId = StarterLessonInstaller.Read(pack.Code).CourseId;
            var words = await database.VocabularyItems.AsNoTracking().Where(item => item.CourseId == courseId).ToArrayAsync();
            Assert.Equal(34, words.Length);
            Assert.All(words, word =>
            {
                Assert.False(string.IsNullOrWhiteSpace(word.Example));
                Assert.False(string.IsNullOrWhiteSpace(word.PartOfSpeech));
                Assert.NotNull(word.Topic);
            });
        }
        Guid studentId = StarterLessonInstaller.ContentId("vocabulary/es/student");
        var student = await database.VocabularyItems.SingleAsync(item => item.Id == studentId);
        student.Word = "el alumno";
        student.EnglishTranslation = "learner";
        student.Example = "Published staff revision.";
        await database.SaveChangesAsync();
        Assert.Equal(0, await firstScope.ServiceProvider.GetRequiredService<StarterVocabularyInstaller>().InstallAsync(default));
        var retained = await database.VocabularyItems.AsNoTracking().SingleAsync(item => item.Id == studentId);
        Assert.Equal("el alumno", retained.Word);
        Assert.Equal("learner", retained.EnglishTranslation);
        Assert.Equal("Published staff revision.", retained.Example);
        Assert.False(await database.VocabularyItems.AnyAsync(item => item.CourseId == student.CourseId && item.Word == "el estudiante"));
        var savedAfter = await database.SavedWords.AsNoTracking().SingleAsync(item => item.Id == saved.Id);
        Assert.Equal(savedBefore.DueAt, savedAfter.DueAt);
        Assert.Equal(savedBefore.IntervalDays, savedAfter.IntervalDays);
        Assert.Equal(savedBefore.CorrectStreak, savedAfter.CorrectStreak);
        Assert.Equal(112, await database.AuditLogs.CountAsync(item => item.Action == "Vocabulary.BuiltInInstalled"));
        Assert.False(await database.XpTransactions.AnyAsync(item => item.UserId == learner.Id));
    }
}
