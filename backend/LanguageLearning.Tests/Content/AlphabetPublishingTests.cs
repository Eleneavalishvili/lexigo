using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Learning;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    private static readonly Guid Russian = Guid.Parse("10000000-0000-0000-0000-000000000004");

    [Fact]
    public async Task RussianLetterRecognitionPublishesAndPreservesYoAsADistinctLetter()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor, 5);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer, 5);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token, Russian), "Alphabet", "Find the Russian letter yo.", ["Е", "Ё", "Э"], ["Ё"]);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await author.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = Russian }, writer.Token)).EnsureSuccessStatusCode();
        var response = await author.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.Id}/sessions", bearer: writer.Token);
        response.EnsureSuccessStatusCode(); var session = (await response.Content.ReadFromJsonAsync<SessionView>())!;
        session.CurrentExercise!.Options.Select(item => item.Text).Should().BeEquivalentTo(["Е", "Ё", "Э"]);
        Guid selected = session.CurrentExercise.Options.Single(item => item.Text == "Ё").Id;
        var answer = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = session.CurrentExercise.Id, selectedOptionIds = new[] { selected }, idempotencyKey = Guid.NewGuid() }, writer.Token);
        answer.EnsureSuccessStatusCode(); (await answer.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect.Should().BeTrue();
        var complete = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: writer.Token);
        complete.EnsureSuccessStatusCode(); (await complete.Content.ReadFromJsonAsync<SessionView>())!.Passed.Should().BeTrue();
        // Revision copying must support this format too.
        (await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{lesson.Id}/versions", bearer: writer.Token)).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("А", "A")]
    [InlineData("А", "АА")]
    [InlineData("А", "1")]
    public async Task AlphabetPublicationRejectsLatinLookalikesAndNonLetterOptions(string first, string second)
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor, 5);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token, Russian), "Alphabet", "Choose a Russian letter.", [first, second], [first]);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RussianAlphabetCannotBePublishedInAnotherLanguageCourse()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), "Alphabet", "Choose a letter.", ["А", "Б"], ["А"]);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
