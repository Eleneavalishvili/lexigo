using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed partial class CompletionCardTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private async Task<(SecurityTestAccount Account, string Token, LearningTestContent Content, Guid Unit)> SetupAsync(SessionTestBrowser browser)
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        string token = await account.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, account.Id, count: 1);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token)).EnsureSuccessStatusCode();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid unit = await db.Lessons.Where(item => item.Id == content.LessonId).Select(item => item.UnitId).SingleAsync();
        return (account, token, content, unit);
    }

    private static async Task<SessionView> FinishAsync(SessionTestBrowser browser, string token, Guid lessonId)
    {
        var response = await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lessonId}/sessions", bearer: token);
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<SessionView>())!;
        var answer = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new { sessionExerciseId = session.CurrentExercise!.Id, answerText = "hola", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, token);
        answer.EnsureSuccessStatusCode();
        var completed = await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token);
        completed.EnsureSuccessStatusCode();
        return (await completed.Content.ReadFromJsonAsync<SessionView>())!;
    }

    private static async Task<OwnedCompletionCard> CardAsync(SessionTestBrowser browser, string token, Guid unit)
    {
        var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/me/completion-cards?unitId=" + unit, bearer: token);
        response.EnsureSuccessStatusCode();
        return Assert.Single((await response.Content.ReadFromJsonAsync<CompletionCardPage>())!.Items);
    }

    private static Task<HttpResponseMessage> ShareAsync(SessionTestBrowser browser, string token, OwnedCompletionCard card, bool enabled, bool consent = true) =>
        browser.SendAsync(HttpMethod.Put, $"/api/v1/me/completion-cards/{card.Id}/sharing",
            new { isShared = enabled, confirmPublicName = consent, revision = card.Revision }, token);

    [Fact]
    public async Task PassingTheUnitIssuesOnePrivateCardAndRepeatedCompletionCannotMintAnother()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(browser);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post,
            $"/api/v1/me/units/{setup.Unit}/completion-card", bearer: setup.Token)).StatusCode);
        var session = await FinishAsync(browser, setup.Token, setup.Content.LessonId);
        var card = await CardAsync(browser, setup.Token, setup.Unit);
        Assert.False(card.IsShared);
        Assert.Null(card.ShareCode);
        Assert.Equal(1, card.Details.LessonCount);
        Assert.Equal(1, card.Details.GradedLessonCount);
        Assert.Equal(100m, card.Details.AverageBestScore);
        Assert.Matches("^[a-f0-9]{32}$", card.Details.VerificationCode);
        var repeats = await Task.WhenAll(browser.SendAsync(HttpMethod.Post,
            $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: setup.Token),
            browser.SendAsync(HttpMethod.Post, $"/api/v1/me/units/{setup.Unit}/completion-card", bearer: setup.Token));
        Assert.All(repeats, item => Assert.Equal(HttpStatusCode.OK, item.StatusCode));
        Assert.Equal(card, await CardAsync(browser, setup.Token, setup.Unit));
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/api/v1/completion-cards/shared/" + card.Details.VerificationCode)).StatusCode);
    }

    [Fact]
    public async Task HistoricUngradedCompletionDoesNotInventAnAccuracyScore()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(browser);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        db.LessonProgress.Add(new LessonProgress
        {
            UserId = setup.Account.Id,
            LessonId = setup.Content.LessonId,
            Status = "Completed",
            BestScore = 0,
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();
        var claims = await Task.WhenAll(browser.SendAsync(HttpMethod.Post, $"/api/v1/me/units/{setup.Unit}/completion-card", bearer: setup.Token),
            browser.SendAsync(HttpMethod.Post, $"/api/v1/me/units/{setup.Unit}/completion-card", bearer: setup.Token));
        using var created = Assert.Single(claims, item => item.StatusCode == HttpStatusCode.Created);
        using var replay = Assert.Single(claims, item => item.StatusCode == HttpStatusCode.OK);
        var card = await CardAsync(browser, setup.Token, setup.Unit);
        Assert.NotNull(created.Headers.Location);
        var detailPath = created.Headers.Location.AbsolutePath;
        Assert.Equal($"/api/v1/me/completion-cards/{card.Id}", detailPath);
        using var detail = await browser.SendAsync(HttpMethod.Get, detailPath, bearer: setup.Token);
        Assert.Equal(card, await detail.Content.ReadFromJsonAsync<OwnedCompletionCard>());
        using var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(card.Id, replayBody.RootElement.GetProperty("id").GetGuid());
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var outsiderAccount = await SecurityTestAccount.CreateAsync(fixture);
        string outsiderToken = await outsiderAccount.LoginAsync(outsider);
        using var hidden = await outsider.SendAsync(HttpMethod.Get, detailPath, bearer: outsiderToken);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Null(card.Details.AverageBestScore);
        Assert.Equal(0, card.Details.GradedLessonCount);
    }

    [Fact]
    public async Task UnitResultSnapshotSurvivesLaterContentEdits()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var setup = await SetupAsync(browser);
        await FinishAsync(browser, setup.Token, setup.Content.LessonId);
        var card = await CardAsync(browser, setup.Token, setup.Unit);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await db.Units.Where(item => item.Id == setup.Unit).ExecuteUpdateAsync(update => update.SetProperty(item => item.Title, "Edited unit title"));
        Assert.Equal(card.Details, (await CardAsync(browser, setup.Token, setup.Unit)).Details);
    }
}
