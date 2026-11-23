using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task DraftMoveSwapsNeighborsOnceAndPreservesAnswerKeys()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var other = Browser();
        var anotherWriter = await StaffAsync(other, RoleNames.ContentAuthor);
        var draft = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token),
            "SentenceBuilder", "Build the sentence.", ["Yo", "bebo", "agua"], [], [[1, 2, 3]]);
        var first = Assert.Single(draft.Exercises);
        var lessonRoute = $"/api/v1/manage/lessons/{draft.Id}";
        Guid second = await IdAsync(await author.SendAsync(HttpMethod.Post, $"{lessonRoute}/exercises", new
        {
            versionId = draft.VersionId,
            type = "Translation",
            prompt = "Translate the sentence.",
            explanation = "Use present tense.",
            orderNumber = 3,
            options = Array.Empty<string>(),
            answers = new[] { "I drink water." }
        }, writer.Token), HttpStatusCode.Created, "/api/v1/manage/exercises");
        var moveRoute = $"{lessonRoute}/exercises/{second}/move";
        var move = new { versionId = draft.VersionId, neighborId = first.Id, earlier = true };

        using var denied = await other.SendAsync(HttpMethod.Post, moveRoute, move, anotherWriter.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var results = await Task.WhenAll(
            author.SendAsync(HttpMethod.Post, moveRoute, move, writer.Token),
            author.SendAsync(HttpMethod.Post, moveRoute, move, writer.Token));
        Assert.Single(results, result => result.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, result => result.StatusCode == HttpStatusCode.Conflict);
        foreach (var result in results) result.Dispose();

        using var response = await author.SendAsync(HttpMethod.Get, lessonRoute, bearer: writer.Token);
        response.EnsureSuccessStatusCode();
        var reordered = (await response.Content.ReadFromJsonAsync<StaffLesson>())!;
        Assert.Equal(new[] { second, first.Id }, reordered.Exercises.Select(exercise => exercise.Id));
        Assert.Equal(new[] { 1, 3 }, reordered.Exercises.Select(exercise => exercise.OrderNumber));
        Assert.Equal(first.Options, reordered.Exercises[1].Options);
        Assert.Equal(first.Answers, reordered.Exercises[1].Answers);
        (await SubmitAsync(author, writer.Token, draft.Id, draft.VersionId)).EnsureSuccessStatusCode();
        using var submitted = await author.SendAsync(HttpMethod.Post,
            $"{lessonRoute}/exercises/{first.Id}/move",
            new { versionId = draft.VersionId, neighborId = second, earlier = true }, writer.Token);
        Assert.Equal(HttpStatusCode.Conflict, submitted.StatusCode);
    }

    [Fact]
    public async Task MatchingPreviewIsPrivateAndDoesNotSaveLearningActivity()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var other = Browser();
        var anotherWriter = await StaffAsync(other, RoleNames.ContentAuthor);
        var draft = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token),
            "Matching", "Connect the greetings to their meanings.", [], [], matchPairs: SpanishPairs);
        var exercise = Assert.Single(draft.Exercises);
        var pairs = SpanishPairs.Select(pair => new MatchedPair(
            exercise.Options.Single(option => option.Text == pair.Left).Id,
            exercise.Options.Single(option => option.Text == pair.Right).Id)).ToArray();
        var route = $"/api/v1/manage/lessons/{draft.Id}/preview/check";
        object Answer(MatchedPair[] matches) => new
        {
            versionId = draft.VersionId,
            exerciseId = exercise.Id,
            matchedPairs = matches
        };

        using var denied = await other.SendAsync(HttpMethod.Post, route, Answer(pairs), anotherWriter.Token);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var incomplete = await author.SendAsync(HttpMethod.Post, route, Answer([pairs[0]]), writer.Token);
        Assert.Equal(HttpStatusCode.NotFound, incomplete.StatusCode);

        var swapped = pairs.Select((pair, index) =>
            new MatchedPair(pair.LeftOptionId, pairs[(index + 1) % pairs.Length].RightOptionId)).ToArray();
        using var wrong = await author.SendAsync(HttpMethod.Post, route, Answer(swapped), writer.Token);
        wrong.EnsureSuccessStatusCode();
        Assert.False((await wrong.Content.ReadFromJsonAsync<PreviewFeedback>())!.Correct);
        using var right = await author.SendAsync(HttpMethod.Post, route, Answer(pairs), writer.Token);
        right.EnsureSuccessStatusCode();
        Assert.True((await right.Content.ReadFromJsonAsync<PreviewFeedback>())!.Correct);

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.False(await database.AnswerAttempts.AnyAsync(item => item.UserId == writer.Account.Id));
        Assert.False(await database.XpTransactions.AnyAsync(item => item.UserId == writer.Account.Id));
    }
}
