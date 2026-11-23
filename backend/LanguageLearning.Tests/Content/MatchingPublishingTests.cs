using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    private static readonly MatchingPairInput[] SpanishPairs = [new("Hola", "Hello"), new("Adiós", "Goodbye"), new("Gracias", "Thank you")];

    private async Task<SessionView> OpenMatchingAsync(SessionTestBrowser author, string token, StaffLesson lesson)
    {
        (await author.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = Spanish }, token)).EnsureSuccessStatusCode();
        var response = await author.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.Id}/sessions", bearer: token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionView>())!;
    }
    private static MatchedPair[] ExpectedPairs(SessionView session) => SpanishPairs.Select(pair => new MatchedPair(
        session.CurrentExercise!.Options.Single(item => item.Side == "left" && item.Text == pair.Left).Id,
        session.CurrentExercise.Options.Single(item => item.Side == "right" && item.Text == pair.Right).Id)).ToArray();

    [Fact]
    public async Task DuplicatedMatchingDraftPreservesPairsWithIndependentKeys()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var draft = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token),
            "Matching", "Connect each greeting with its meaning.", [], [], matchPairs: SpanishPairs);
        Guid copyId = await IdAsync(await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{draft.Id}/duplicate",
            new { versionId = draft.VersionId, title = "Matching copy" }, writer.Token), HttpStatusCode.Created);
        using var response = await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{copyId}", bearer: writer.Token);
        response.EnsureSuccessStatusCode();
        var copy = (await response.Content.ReadFromJsonAsync<StaffLesson>())!;
        var sourceOptions = Assert.Single(draft.Exercises).Options;
        var copiedOptions = Assert.Single(copy.Exercises).Options;
        copiedOptions.Select(item => item.MatchKey).Should().NotIntersectWith(sourceOptions.Select(item => item.MatchKey));
        foreach (var pair in SpanishPairs)
        {
            var left = copiedOptions.Single(item => item.Text == pair.Left);
            var right = copiedOptions.Single(item => item.Text == pair.Right);
            left.MatchKey.Should().NotBeNull().And.Be(right.MatchKey);
        }
        (await SubmitAsync(author, writer.Token, copy.Id, copy.VersionId)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MatchingPublicationHidesKeysAndCanonicalReplayRewardsOnlyOneCompletion()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), "Matching", "Connect each greeting with its meaning.", [], [], matchPairs: SpanishPairs);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        var session = await OpenMatchingAsync(author, writer.Token, lesson);
        var publicResponse = await author.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: writer.Token);
        string json = await publicResponse.Content.ReadAsStringAsync();
        json.Should().NotContain("matchKey").And.NotContain("matchingPairs").And.NotContain("evaluationRules").And.NotContain("orderNumber");
        foreach (var key in lesson.Exercises[0].Options.Select(item => item.MatchKey).Distinct()) json.Should().NotContain(key!);
        session.CurrentExercise!.Options.Count(item => item.Side == "left").Should().Be(3);
        Guid requestKey = Guid.NewGuid(); var pairs = ExpectedPairs(session);
        object Body(MatchedPair[] value) => new { sessionExerciseId = session.CurrentExercise.Id, selectedOptionIds = Array.Empty<Guid>(), matchedPairs = value, idempotencyKey = requestKey };
        var responses = await Task.WhenAll(author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", Body(pairs), writer.Token),
            author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", Body(pairs.Reverse().ToArray()), writer.Token));
        foreach (var response in responses) { response.EnsureSuccessStatusCode(); (await response.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect.Should().BeTrue(); }
        (await responses[0].Content.ReadAsStringAsync()).Should().Be(await responses[1].Content.ReadAsStringAsync());
        var completions = await Task.WhenAll(author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: writer.Token), author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: writer.Token));
        foreach (var response in completions) { response.EnsureSuccessStatusCode(); (await response.Content.ReadFromJsonAsync<SessionView>())!.AwardedXp.Should().Be(20); }
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.AnswerAttempts.CountAsync(item => item.UserId == writer.Account.Id)).Should().Be(1);
        (await db.XpTransactions.CountAsync(item => item.UserId == writer.Account.Id)).Should().Be(1);
    }

    [Fact]
    public async Task InvalidPairShapesSaveNothingButCompleteWrongMappingCostsOneHeart()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), "Matching", "Connect words.", [], [], matchPairs: SpanishPairs);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        var session = await OpenMatchingAsync(author, writer.Token, lesson); var pairs = ExpectedPairs(session);
        MatchedPair[][] invalid = [pairs.Take(2).ToArray(), [pairs[0], pairs[0], pairs[2]], [new(pairs[0].LeftOptionId, Guid.NewGuid()), pairs[1], pairs[2]], pairs.Select(pair => new MatchedPair(pair.RightOptionId, pair.LeftOptionId)).ToArray(), [null!, pairs[1], pairs[2]]];
        foreach (var value in invalid)
        {
            (await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise!.Id, selectedOptionIds = Array.Empty<Guid>(), matchedPairs = value, idempotencyKey = Guid.NewGuid() }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        var unchanged = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: writer.Token)).Content.ReadFromJsonAsync<SessionView>())!;
        unchanged.Hearts.Should().Be(5); unchanged.AnsweredCount.Should().Be(0);
        var wrong = pairs.Select((pair, index) => new MatchedPair(pair.LeftOptionId, pairs[(index + 1) % pairs.Length].RightOptionId)).ToArray();
        Guid key = Guid.NewGuid();
        var response = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise!.Id, selectedOptionIds = Array.Empty<Guid>(), matchedPairs = wrong, idempotencyKey = key }, writer.Token);
        response.EnsureSuccessStatusCode(); var feedback = (await response.Content.ReadFromJsonAsync<AnswerFeedback>())!;
        feedback.IsCorrect.Should().BeFalse(); feedback.HeartsRemaining.Should().Be(4);
        (await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise.Id, selectedOptionIds = Array.Empty<Guid>(), matchedPairs = pairs, idempotencyKey = key }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MatchingRevisionRegeneratesPrivateKeysAndOldSessionKeepsItsOriginalPairs()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), "Matching", "Connect words.", [], [], matchPairs: SpanishPairs);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        var session = await OpenMatchingAsync(author, writer.Token, lesson); var originalPairs = ExpectedPairs(session);
        Guid revision = await RevisionAsync(author, writer.Token, lesson.Id);
        var copy = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        copy.Exercises[0].Options.Select(item => item.Id).Should().NotIntersectWith(lesson.Exercises[0].Options.Select(item => item.Id));
        copy.Exercises[0].Options.Select(item => item.MatchKey).Should().NotIntersectWith(lesson.Exercises[0].Options.Select(item => item.MatchKey));
        (await SubmitAsync(author, writer.Token, lesson.Id, revision)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, revision)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, revision)).EnsureSuccessStatusCode();
        var response = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise!.Id, selectedOptionIds = Array.Empty<Guid>(), matchedPairs = originalPairs, idempotencyKey = Guid.NewGuid() }, writer.Token);
        response.EnsureSuccessStatusCode(); (await response.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task AmbiguousLabelsAndBrokenPrivatePairsCannotReachPublication()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), "Matching", "Connect words.", [], [], matchPairs: SpanishPairs);
        MatchingPairInput[][] invalid = [[new("Hola", "Hello")], [new("Hola", "Hello"), new(" hola ", "Goodbye")], [new("Hola", "Hello"), new("Adiós", "HELLO")], [null!, new("Adiós", "Goodbye")]];
        foreach (var pairs in invalid)
        {
            (await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{lesson.Id}/exercises", new { versionId = lesson.VersionId, type = "Matching", prompt = "Connect", explanation = "English feedback", orderNumber = 2, options = Array.Empty<string>(), answers = Array.Empty<string>(), matchPairs = pairs }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid optionId = lesson.Exercises[0].Options[0].Id;
        await db.ExerciseOptions.Where(item => item.Id == optionId).ExecuteUpdateAsync(update => update.SetProperty(item => item.MatchKey, "broken-private-key"));
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
