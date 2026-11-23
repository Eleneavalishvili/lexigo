using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Vocabulary;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Vocabulary;

public sealed class VocabularyTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private static readonly Guid CourseId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private SessionTestBrowser Browser() => new(fixture.CreateIsolatedClient());

    private async Task<VocabularyItem> WordAsync(string? text = null, bool published = true)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.Courses.SingleAsync(item => item.Id == CourseId)).IsPublished = published;
        var word = new VocabularyItem { CourseId = CourseId, Word = text ?? "Hola-" + Guid.NewGuid(), EnglishTranslation = "Hello-" + Guid.NewGuid(), Example = "Hola, ¿cómo estás?", PartOfSpeech = "interjection" };
        db.VocabularyItems.Add(word);
        await db.SaveChangesAsync();
        return word;
    }

    [Fact]
    public async Task WordAudioRequiresPublishedCourseAndReviewedSameLanguageRecording()
    {
        using var browser = Browser();
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var reviewer = await SecurityTestAccount.CreateAsync(fixture);
        var token = await user.LoginAsync(browser);
        var word = await WordAsync();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var clip = new MediaAsset
        {
            UploadedBy = user.Id,
            StorageKey = $"media/{Guid.NewGuid():N}",
            MimeType = "audio/wav",
            ByteSize = 2000,
            LanguageId = 2,
            Source = "Vocabulary test",
            License = "Original test recording",
            DurationMilliseconds = 200,
            Sha256 = new string('a', 64)
        };
        db.MediaAssets.Add(clip);
        (await db.VocabularyItems.SingleAsync(item => item.Id == word.Id)).AudioMediaId = clip.Id;
        await db.SaveChangesAsync();
        string path = $"/api/v1/vocabulary/{word.Id}/audio";
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Get, path)).StatusCode);

        async Task<WordView> CatalogWord()
        {
            var response = await browser.SendAsync(HttpMethod.Get,
                "/api/v1/vocabulary?search=" + Uri.EscapeDataString(word.Word), bearer: token);
            response.EnsureSuccessStatusCode();
            return Assert.Single((await response.Content.ReadFromJsonAsync<WordPage>())!.Items);
        }

        Assert.False((await CatalogWord()).HasAudio);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.SendAsync(HttpMethod.Get, path, bearer: token)).StatusCode);

        clip.IsVerified = true;
        clip.VerifiedBy = reviewer.Id;
        clip.VerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Assert.True((await CatalogWord()).HasAudio);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await browser.SendAsync(HttpMethod.Get, path, bearer: token)).StatusCode);

        clip.LanguageId = 3;
        await db.SaveChangesAsync();
        Assert.False((await CatalogWord()).HasAudio);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.SendAsync(HttpMethod.Get, path, bearer: token)).StatusCode);

        clip.LanguageId = 2;
        (await db.Courses.SingleAsync(item => item.Id == CourseId)).IsPublished = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await browser.SendAsync(HttpMethod.Get, path, bearer: token)).StatusCode);
    }

    [Fact]
    public async Task TopicFiltersComposeWithCourseSearchAndOwnedReviewSchedule()
    {
        using var browser = Browser();
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var token = await user.LoginAsync(browser);
        await WordAsync();
        var response = await browser.SendAsync(HttpMethod.Get,
            $"/api/v1/vocabulary?topic=food&courseId={CourseId}&search=bread", bearer: token);
        response.EnsureSuccessStatusCode();
        var bread = Assert.Single((await response.Content.ReadFromJsonAsync<WordPage>())!.Items);
        Assert.Equal("el pan", bread.Word);
        Assert.Equal("food", bread.Topic);

        var saved = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words",
            new { vocabularyItemId = bread.Id }, token);
        saved.EnsureSuccessStatusCode();
        const string savedPath = "/api/v1/me/saved-words?status=due&topic=";
        var food = await browser.SendAsync(HttpMethod.Get, savedPath + "food", bearer: token);
        Assert.Equal(bread.Id, Assert.Single((await food.Content.ReadFromJsonAsync<WordPage>())!.Items).Id);
        var travel = await browser.SendAsync(HttpMethod.Get, savedPath + "travel", bearer: token);
        Assert.Empty((await travel.Content.ReadFromJsonAsync<WordPage>())!.Items);

        using var otherBrowser = Browser();
        var other = await SecurityTestAccount.CreateAsync(fixture);
        var otherToken = await other.LoginAsync(otherBrowser);
        var otherFood = await otherBrowser.SendAsync(HttpMethod.Get, savedPath + "food", bearer: otherToken);
        Assert.Empty((await otherFood.Content.ReadFromJsonAsync<WordPage>())!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get,
            "/api/v1/vocabulary?topic=food%25", bearer: token)).StatusCode);
    }

    [Fact]
    public async Task CollectionsArePrivateAndDeletingOnePreservesSavedWords()
    {
        using var browser = Browser();
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var token = await user.LoginAsync(browser);
        using var foreignBrowser = Browser();
        var foreign = await SecurityTestAccount.CreateAsync(fixture);
        var foreignToken = await foreign.LoginAsync(foreignBrowser);
        var word = await WordAsync();
        var saved = (await (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words",
            new { vocabularyItemId = word.Id }, token)).Content.ReadFromJsonAsync<SaveWordResult>())!;
        const string collections = "/api/v1/me/collections";
        var input = new { courseId = CourseId, name = "Travel" };
        var attempts = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Post, collections, input, token),
            browser.SendAsync(HttpMethod.Post, collections, input, token));
        using var create = Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Created);
        using var replay = Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.OK);
        var id = (await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(id, (await replay.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid());
        var location = create.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal($"{collections}/{id}", location.AbsolutePath);
        using var detail = await browser.SendAsync(HttpMethod.Get, location.AbsolutePath, bearer: token);
        detail.EnsureSuccessStatusCode();
        Assert.Equal(new WordCollectionView(id, CourseId, "Travel", 0),
            await detail.Content.ReadFromJsonAsync<WordCollectionView>());
        using var foreignDetail = await foreignBrowser.SendAsync(HttpMethod.Get, location.AbsolutePath, bearer: foreignToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignDetail.StatusCode);
        var member = $"{collections}/{id}/words/{saved.Id}";
        Assert.Equal(HttpStatusCode.NotFound, (await foreignBrowser.SendAsync(HttpMethod.Post, member, bearer: foreignToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Post, member, bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Post, member, bearer: token)).StatusCode);
        var own = await browser.SendAsync(HttpMethod.Get, collections, bearer: token);
        Assert.Equal(1, Assert.Single((await own.Content.ReadFromJsonAsync<WordCollectionView[]>())!).WordCount);
        var other = await foreignBrowser.SendAsync(HttpMethod.Get, collections, bearer: foreignToken);
        Assert.Empty((await other.Content.ReadFromJsonAsync<WordCollectionView[]>())!);
        var filtered = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me/saved-words?collectionId={id}", bearer: token);
        Assert.Equal(saved.Id, Assert.Single((await filtered.Content.ReadFromJsonAsync<WordPage>())!.Items).SavedWordId);
        var graded = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/saved-words/{saved.Id}/reviews",
            new { idempotencyKey = Guid.NewGuid(), answer = word.EnglishTranslation }, token);
        graded.EnsureSuccessStatusCode();
        const string download = "/api/v1/me/export/vocabulary";
        var export = await browser.SendAsync(HttpMethod.Get, download, bearer: token);
        export.EnsureSuccessStatusCode();
        Assert.True(export.Headers.CacheControl?.NoStore);
        Assert.Equal("attachment", export.Content.Headers.ContentDisposition?.DispositionType);
        var data = await export.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(saved.Id, data.GetProperty("savedWords")[0].GetProperty("id").GetGuid());
        Assert.True(data.GetProperty("reviews")[0].GetProperty("isCorrect").GetBoolean());
        Assert.False(data.GetProperty("reviews")[0].TryGetProperty("idempotencyKey", out _));
        Assert.Equal("Travel", data.GetProperty("collections")[0].GetProperty("name").GetString());
        Assert.Equal(id, data.GetProperty("collectionWords")[0].GetProperty("collectionId").GetGuid());
        var foreignExport = await foreignBrowser.SendAsync(HttpMethod.Get, download, bearer: foreignToken);
        foreignExport.EnsureSuccessStatusCode();
        var foreignData = await foreignExport.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        foreach (string section in new[] { "savedWords", "reviews", "collections", "collectionWords" })
            Assert.Equal(0, foreignData.GetProperty(section).GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(download)).StatusCode);
        var rename = await browser.SendAsync(HttpMethod.Put, $"{collections}/{id}", new { name = "At the airport" }, token);
        rename.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Delete, member, bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Post, member, bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Delete, $"{collections}/{id}", bearer: token)).StatusCode);
        var remaining = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/saved-words", bearer: token);
        Assert.Equal(saved.Id, Assert.Single((await remaining.Content.ReadFromJsonAsync<WordPage>())!.Items).SavedWordId);
    }

    [Fact]
    public async Task ReviewsAdvanceAllIntervalsAndResetAfterAnIncorrectAnswer()
    {
        using var browser = Browser();
        var user = await SecurityTestAccount.CreateAsync(fixture);
        var token = await user.LoginAsync(browser);
        var word = await WordAsync();
        var saved = (await (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words",
            new { vocabularyItemId = word.Id }, token)).Content.ReadFromJsonAsync<SaveWordResult>())!;
        var path = $"/api/v1/me/saved-words/{saved.Id}/reviews";
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        using var foreignBrowser = Browser();
        var foreign = await SecurityTestAccount.CreateAsync(fixture);
        var foreignToken = await foreign.LoginAsync(foreignBrowser);
        Assert.Equal(HttpStatusCode.NotFound, (await foreignBrowser.SendAsync(HttpMethod.Post, path,
            new { idempotencyKey = Guid.NewGuid(), answer = word.EnglishTranslation }, foreignToken)).StatusCode);

        foreach (var interval in new[] { 1, 3, 7, 14, 30 })
        {
            var key = Guid.NewGuid();
            var input = new { idempotencyKey = key, answer = word.EnglishTranslation };
            var responses = await Task.WhenAll(
                browser.SendAsync(HttpMethod.Post, path, input, token),
                browser.SendAsync(HttpMethod.Post, path, input, token));
            var reviews = await Task.WhenAll(responses.Select(async response =>
            {
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<WordReviewResult>())!;
            }));
            Assert.Equal(reviews[0], reviews[1]);
            Assert.True(reviews[0].IsCorrect);
            Assert.Equal(interval, reviews[0].IntervalDays);
            Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, path,
                new { idempotencyKey = Guid.NewGuid(), answer = word.EnglishTranslation }, token)).StatusCode);
            await database.SavedWords.Where(item => item.Id == saved.Id).ExecuteUpdateAsync(
                update => update.SetProperty(item => item.DueAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }
        var wrong = await browser.SendAsync(HttpMethod.Post, path,
            new { idempotencyKey = Guid.NewGuid(), answer = "incorrect" }, token);
        wrong.EnsureSuccessStatusCode();
        var result = (await wrong.Content.ReadFromJsonAsync<WordReviewResult>())!;
        Assert.False(result.IsCorrect);
        Assert.InRange(result.DueAt, DateTimeOffset.UtcNow.AddMinutes(9), DateTimeOffset.UtcNow.AddMinutes(11));
        Assert.Equal(0, await database.SavedWords.Where(item => item.Id == saved.Id).Select(item => item.CorrectStreak).SingleAsync());
        Assert.Equal(6, await database.WordReviews.CountAsync(item => item.SavedWordId == saved.Id));
        Assert.False(await database.XpTransactions.AnyAsync(item => item.UserId == user.Id));
        Assert.False(await database.DailyActivities.AnyAsync(item => item.UserId == user.Id));
    }

    [Fact]
    public async Task CatalogSearchesBothLanguagesAndTreatsWildcardsAsLiteralText()
    {
        using var browser = Browser(); var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(browser);
        var word = await WordAsync("100%_" + Guid.NewGuid());
        foreach (string search in new[] { word.Word, word.EnglishTranslation, "100%_" })
        {
            var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/vocabulary?search=" + Uri.EscapeDataString(search), bearer: token);
            response.EnsureSuccessStatusCode();
            var page = (await response.Content.ReadFromJsonAsync<WordPage>())!;
            Assert.Contains(page.Items, item => item.Id == word.Id && item.SavedWordId == null && item.LanguageCode == "es");
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        var other = (await (await browser.SendAsync(HttpMethod.Get, "/api/v1/vocabulary?courseId=" + Guid.NewGuid(), bearer: token)).Content.ReadFromJsonAsync<WordPage>())!;
        Assert.Empty(other.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, "/api/v1/vocabulary?page=0", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, "/api/v1/vocabulary?search=" + new string('a', 161), bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/saved-words?status=wrong", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, "/api/v1/vocabulary?search=%00", bearer: token)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentSavesKeepOneOwnedScheduleAndNeverCreateXp()
    {
        using var browser = Browser(); var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(browser);
        var word = await WordAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = word.Id }, token)));
        var created = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        var saved = await Task.WhenAll(responses.Select(async response => { response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<SaveWordResult>())!; }));
        Assert.Single(saved.Select(item => item.Id).Distinct());
        Assert.Single(saved.Select(item => item.DueAt).Distinct());
        var location = created.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal($"/api/v1/me/saved-words/{saved[0].Id}", location.AbsolutePath);
        using var detail = await browser.SendAsync(HttpMethod.Get, location.AbsolutePath, bearer: token);
        detail.EnsureSuccessStatusCode();
        Assert.Equal(saved[0], await detail.Content.ReadFromJsonAsync<SaveWordResult>());
        Assert.All(saved, item => { Assert.Equal(0, item.IntervalDays); Assert.Equal(0, item.CorrectStreak); });
        var page = (await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/saved-words?status=due", bearer: token)).Content.ReadFromJsonAsync<WordPage>())!;
        Assert.Equal(saved[0].Id, Assert.Single(page.Items).SavedWordId);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await db.SavedWords.CountAsync(item => item.UserId == user.Id));
        Assert.False(await db.XpTransactions.AnyAsync(item => item.UserId == user.Id));
        Assert.False(await db.DailyActivities.AnyAsync(item => item.UserId == user.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = word.Id, userId = Guid.NewGuid(), correctStreak = 30 }, token)).StatusCode);
    }

    [Fact]
    public async Task OtherAccountsCannotSeeOrRemoveSavedWordsAndRemovalPreservesCatalog()
    {
        using var owner = Browser(); var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(owner);
        using var stranger = Browser(); var other = await SecurityTestAccount.CreateAsync(fixture); string otherToken = await other.LoginAsync(stranger);
        var word = await WordAsync();
        var saved = (await (await owner.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = word.Id }, token)).Content.ReadFromJsonAsync<SaveWordResult>())!;
        using var foreignDetail = await stranger.SendAsync(HttpMethod.Get, $"/api/v1/me/saved-words/{saved.Id}", bearer: otherToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignDetail.StatusCode);
        Assert.Empty((await (await stranger.SendAsync(HttpMethod.Get, "/api/v1/me/saved-words", bearer: otherToken)).Content.ReadFromJsonAsync<WordPage>())!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.SendAsync(HttpMethod.Delete, "/api/v1/me/saved-words/" + saved.Id, bearer: otherToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.SendAsync(HttpMethod.Delete, "/api/v1/me/saved-words/" + saved.Id, bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.SendAsync(HttpMethod.Delete, "/api/v1/me/saved-words/" + saved.Id, bearer: token)).StatusCode);
        using var scope = fixture.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().VocabularyItems.AnyAsync(item => item.Id == word.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/v1/vocabulary")).StatusCode);
    }

    [Fact]
    public async Task WithdrawnCoursesHideCatalogAndNewSavesButKeepPersonalLibrary()
    {
        using var browser = Browser(); var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(browser);
        var word = await WordAsync();
        var saved = (await (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = word.Id }, token)).Content.ReadFromJsonAsync<SaveWordResult>())!;
        var hidden = await WordAsync(published: false);
        var catalog = (await (await browser.SendAsync(HttpMethod.Get, "/api/v1/vocabulary?courseId=" + CourseId, bearer: token)).Content.ReadFromJsonAsync<WordPage>())!;
        Assert.Empty(catalog.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = hidden.Id }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words", new { vocabularyItemId = word.Id }, token)).StatusCode);
        Assert.Equal(saved.Id, Assert.Single((await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/saved-words", bearer: token)).Content.ReadFromJsonAsync<WordPage>())!.Items).SavedWordId);
    }

    [Fact]
    public async Task ListsAreBoundedAndDueFilteringUsesTheServerSchedule()
    {
        using var browser = Browser(); var user = await SecurityTestAccount.CreateAsync(fixture); string token = await user.LoginAsync(browser);
        string prefix = "page-" + Guid.NewGuid();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.Courses.SingleAsync(item => item.Id == CourseId)).IsPublished = true;
        for (int index = 0; index < 25; index++)
        {
            var word = new VocabularyItem { CourseId = CourseId, Word = prefix + index.ToString("D2"), EnglishTranslation = "Pagination fixture" };
            db.VocabularyItems.Add(word);
            db.SavedWords.Add(new SavedWord { UserId = user.Id, VocabularyItemId = word.Id, DueAt = DateTimeOffset.UtcNow.AddDays(index % 2 == 0 ? -1 : 1) });
        }
        await db.SaveChangesAsync();
        async Task<WordPage> Page(string path) => (await (await browser.SendAsync(HttpMethod.Get, path, bearer: token)).Content.ReadFromJsonAsync<WordPage>())!;
        var first = await Page("/api/v1/vocabulary?search=" + prefix);
        var second = await Page("/api/v1/vocabulary?search=" + prefix + "&page=2");
        Assert.Equal(25, first.TotalCount); Assert.Equal(20, first.Items.Length); Assert.Equal(5, second.Items.Length);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        Assert.Equal(13, (await Page("/api/v1/me/saved-words?status=due")).TotalCount);
        Assert.Equal(12, (await Page("/api/v1/me/saved-words?status=scheduled")).TotalCount);
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateSchedulesInvalidIntervalsAndMissingReferences()
    {
        var user = await SecurityTestAccount.CreateAsync(fixture); var word = await WordAsync();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var saved = new SavedWord { UserId = user.Id, VocabularyItemId = word.Id, DueAt = DateTimeOffset.UtcNow };
        db.SavedWords.Add(saved); await db.SaveChangesAsync();
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"SavedWords\" (\"Id\",\"UserId\",\"VocabularyItemId\",\"DueAt\",\"IntervalDays\",\"CorrectStreak\") VALUES ({Guid.NewGuid()},{user.Id},{word.Id},{DateTimeOffset.UtcNow},0,0)"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"SavedWords\" SET \"IntervalDays\" = 2 WHERE \"Id\" = {saved.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        var missing = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"VocabularyItems\" SET \"AudioMediaId\" = {Guid.NewGuid()} WHERE \"Id\" = {word.Id}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, missing.SqlState);
    }
}
