using LanguageLearning.Core.Accounts;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    private async Task<StaffLesson> TextDraftAsync(SessionTestBrowser browser, string token, Guid unit, string type, string prompt,
        string[] options, string[] answers, int[][]? tokenOrders = null, MatchingPairInput[]? matchPairs = null, string? contextText = null)
    {
        Guid lessonId = await IdAsync(await browser.SendAsync(HttpMethod.Post, "/api/v1/manage/lessons", new { unitId = unit, title = "Build a sentence", objective = "Use everyday words", explanation = "English grammar notes", orderNumber = 1 }, token), HttpStatusCode.Created);
        var lesson = (await (await browser.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lessonId}", bearer: token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{lessonId}/exercises", new { versionId = lesson.VersionId, type, prompt, explanation = "The words follow this order.", orderNumber = 1, options, answers, tokenOrders, matchPairs, contextText }, token)).EnsureSuccessStatusCode();
        return (await (await browser.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lessonId}", bearer: token)).Content.ReadFromJsonAsync<StaffLesson>())!;
    }

    [Fact]
    public async Task OnlyTheDraftOwnerCanDuplicateAndTokenAnswersUseTheNewTiles()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var otherAuthor = Browser();
        var otherWriter = await StaffAsync(otherAuthor, RoleNames.ContentAuthor);
        var draft = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token),
            "SentenceBuilder", "Write this in English: té", ["tea", "and", "milk"], [], [[1]]);
        var route = $"/api/v1/manage/lessons/{draft.Id}/duplicate";
        var copyRequest = new { versionId = draft.VersionId, title = "Separate tea lesson" };

        using var forbidden = await otherAuthor.SendAsync(HttpMethod.Post, route, copyRequest, otherWriter.Token);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var stale = await author.SendAsync(HttpMethod.Post, route,
            copyRequest with { versionId = Guid.NewGuid() }, writer.Token);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var created = await author.SendAsync(HttpMethod.Post, route, copyRequest, writer.Token);
        Guid copyId = await IdAsync(created, HttpStatusCode.Created);
        copyId.Should().NotBe(draft.Id);
        var location = created.Headers.Location!.AbsolutePath;
        using var response = await author.SendAsync(HttpMethod.Get, location, bearer: writer.Token);
        using var anonymous = await fixture.Client.GetAsync(location);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var other = await otherAuthor.SendAsync(HttpMethod.Get, location, bearer: otherWriter.Token);
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.EnsureSuccessStatusCode();
        var copy = (await response.Content.ReadFromJsonAsync<StaffLesson>())!;
        copy.VersionStatus.Should().Be("Draft");
        var exercise = copy.Exercises.Should().ContainSingle().Subject;
        exercise.Id.Should().NotBe(draft.Exercises[0].Id);
        var answer = SentenceAnswerKeys.Read(exercise.Answers.Should().ContainSingle().Subject.OrderedTokenIds)!;
        answer.Should().Equal(exercise.Options.Single(option => option.Text == "tea").Id);
        answer.Should().NotIntersectWith(draft.Exercises[0].Options.Select(option => option.Id));
        (await SubmitAsync(author, writer.Token, copy.Id, copy.VersionId)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SentenceKeysAreRemappedOnRevisionAndOldSessionStillUsesOriginalTiles()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), "SentenceBuilder", "Build: I would like coffee, please.",
            ["café", "Quiero", "por", "favor"], [], [[2, 1, 3, 4], [2, 3, 4, 1]]);
        Guid[] originalOrder = SentenceAnswerKeys.Read(lesson.Exercises[0].Answers[0].OrderedTokenIds)!;
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await author.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = Spanish }, writer.Token)).EnsureSuccessStatusCode();
        var start = await author.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.Id}/sessions", bearer: writer.Token);
        string publicJson = await start.Content.ReadAsStringAsync();
        publicJson.Should().NotContain("acceptedTokenOrders").And.NotContain("orderedTokenIds").And.NotContain("evaluationRules");
        var session = (await start.Content.ReadFromJsonAsync<SessionView>())!;
        session.CurrentExercise!.Options.Select(item => item.Id).Should().BeEquivalentTo(originalOrder);
        Guid revision = await RevisionAsync(author, writer.Token, lesson.Id);
        var revised = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        var copiedOrder = SentenceAnswerKeys.Read(revised.Exercises[0].Answers[0].OrderedTokenIds)!;
        copiedOrder.Should().NotIntersectWith(originalOrder);
        copiedOrder.Should().BeEquivalentTo(revised.Exercises[0].Options.Select(item => item.Id));
        revised.Exercises[0].Answers.Should().HaveCount(2);
        (await SubmitAsync(author, writer.Token, lesson.Id, revision)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, revision)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, revision)).EnsureSuccessStatusCode();
        var wrongVersion = new { sessionExerciseId = session.CurrentExercise.Id, answerText = (string?)null, selectedOptionIds = Array.Empty<Guid>(), orderedTokenIds = copiedOrder, idempotencyKey = Guid.NewGuid() };
        (await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", wrongVersion, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise.Id, selectedOptionIds = Array.Empty<Guid>(), orderedTokenIds = new[] { originalOrder[0], originalOrder[0], originalOrder[2], originalOrder[3] }, idempotencyKey = Guid.NewGuid() }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        Guid key = Guid.NewGuid();
        var answer = new { sessionExerciseId = session.CurrentExercise.Id, answerText = (string?)null, selectedOptionIds = Array.Empty<Guid>(), orderedTokenIds = originalOrder, idempotencyKey = key };
        var responses = await Task.WhenAll(author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", answer, writer.Token), author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", answer, writer.Token));
        foreach (var response in responses) { response.EnsureSuccessStatusCode(); (await response.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect.Should().BeTrue(); }
        (await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise.Id, selectedOptionIds = Array.Empty<Guid>(), orderedTokenIds = originalOrder.Reverse().ToArray(), idempotencyKey = key }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var completed = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: writer.Token);
        completed.EnsureSuccessStatusCode(); (await completed.Content.ReadFromJsonAsync<SessionView>())!.AwardedXp.Should().Be(20);
        var restarted = (await (await author.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.Id}/sessions", bearer: writer.Token)).Content.ReadFromJsonAsync<SessionView>())!;
        var wrong = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{restarted.Id}/answers", new { sessionExerciseId = restarted.CurrentExercise!.Id, selectedOptionIds = Array.Empty<Guid>(), orderedTokenIds = copiedOrder.Reverse().ToArray(), idempotencyKey = Guid.NewGuid() }, writer.Token);
        wrong.EnsureSuccessStatusCode(); var wrongFeedback = (await wrong.Content.ReadFromJsonAsync<AnswerFeedback>())!;
        wrongFeedback.IsCorrect.Should().BeFalse(); wrongFeedback.HeartsRemaining.Should().Be(4);
    }

    [Fact]
    public async Task InvalidTokenOrdersAndForeignPublishedKeysAreRejected()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), "SentenceBuilder", "Build the sentence", ["Ich", "trinke", "Wasser"], [], [[1, 2, 3]]);
        var route = $"/api/v1/manage/lessons/{lesson.Id}/exercises";
        foreach (var badOrder in new[] { new[] { 1, 1, 3 }, Array.Empty<int>(), new[] { 0, 2, 3 }, new[] { 1, 2, 4 } })
        {
            (await author.SendAsync(HttpMethod.Post, route, new { versionId = lesson.VersionId, type = "SentenceBuilder", prompt = "Build", explanation = "English explanation", orderNumber = 2, options = new[] { "Ich", "trinke", "Wasser" }, answers = Array.Empty<string>(), tokenOrders = new[] { badOrder } }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        (await author.SendAsync(HttpMethod.Post, route, new { versionId = lesson.VersionId, type = "SentenceBuilder", prompt = "Build", explanation = "English explanation", orderNumber = 2, options = new[] { "Ich", "trinke", "Wasser" }, answers = Array.Empty<string>(), tokenOrders = new int[]?[] { null } }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        string invalid = JsonSerializer.Serialize(new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });
        await db.AcceptedAnswers.Where(item => item.ExerciseId == lesson.Exercises[0].Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.OrderedTokenIds, invalid));
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task WordBankWithDistractorsIsPublishedGradedAndCopiedWithoutLeakingItsAnswer()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token),
            "SentenceBuilder", "Write this in English: té", ["tea", "and", "milk"], [], [[1]]);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await author.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = Spanish }, writer.Token)).EnsureSuccessStatusCode();
        var start = await author.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.Id}/sessions", bearer: writer.Token);
        start.EnsureSuccessStatusCode();
        var session = (await start.Content.ReadFromJsonAsync<SessionView>())!;
        (await start.Content.ReadAsStringAsync()).Should().NotContain("orderedTokenIds");
        var tea = session.CurrentExercise!.Options.Single(item => item.Text == "tea");
        var response = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new { sessionExerciseId = session.CurrentExercise.Id, selectedOptionIds = Array.Empty<Guid>(), orderedTokenIds = new[] { tea.Id }, idempotencyKey = Guid.NewGuid() }, writer.Token);
        response.EnsureSuccessStatusCode();
        (await response.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect.Should().BeTrue();
        var revision = await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{lesson.Id}/versions", bearer: writer.Token);
        revision.EnsureSuccessStatusCode();
        var copied = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        var order = JsonSerializer.Deserialize<Guid[]>(copied.Exercises[0].Answers[0].OrderedTokenIds!)!;
        order.Should().HaveCount(1).And.NotContain(tea.Id);
        copied.Exercises[0].Options.Single(item => item.Id == order[0]).Text.Should().Be("tea");
    }

    [Fact]
    public async Task FillBlankRequiresOnePlaceholderAndAcceptsPrivateAlternativeAnswers()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), "FillBlank", "Complete: Yo ____ agua.", [], ["bebo", "tomo"]);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await author.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = Spanish }, writer.Token)).EnsureSuccessStatusCode();
        var response = await author.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.Id}/sessions", bearer: writer.Token);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("bebo").And.NotContain("tomo");
        var session = (await response.Content.ReadFromJsonAsync<SessionView>())!;
        var feedback = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise!.Id, answerText = "  TOMO  ", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, writer.Token);
        feedback.EnsureSuccessStatusCode(); (await feedback.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect.Should().BeTrue();
        var invalid = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), "FillBlank", "There is no blank here", [], ["tomo"]);
        (await SubmitAsync(author, writer.Token, invalid.Id, invalid.VersionId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
