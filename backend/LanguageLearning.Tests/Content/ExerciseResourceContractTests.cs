using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task NewExerciseLocationsRespectVersionOwnershipReviewScopeAndRemoval()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var other = Browser();
        var stranger = await StaffAsync(other, RoleNames.ContentAuthor);
        using var reviewer = Browser();
        var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        using var foreign = Browser();
        var german = await StaffAsync(foreign, RoleNames.Reviewer, 4);
        var lesson = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        using var created = await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{lesson.Id}/exercises", new
        {
            versionId = lesson.Version, type = "Translation", prompt = "Say goodbye in Spanish",
            explanation = "Adiós is goodbye.", orderNumber = 2, options = Array.Empty<string>(), answers = new[] { "Adiós" }
        }, writer.Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<ExerciseResourceId>())!.Id;
        Assert.Equal($"/api/v1/manage/exercises/{id}", created.Headers.Location!.AbsolutePath);
        string route = created.Headers.Location.PathAndQuery;
        using var owned = await author.SendAsync(HttpMethod.Get, route, bearer: writer.Token);
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
        Assert.Contains("no-store", owned.Headers.CacheControl!.ToString());
        var detail = (await owned.Content.ReadFromJsonAsync<ManagedExercise>())!;
        Assert.Equal(lesson.Id, detail.LessonId);
        Assert.Equal(lesson.Version, detail.VersionId);
        Assert.Equal(id, detail.Exercise.Id);
        Assert.Equal("Adiós", Assert.Single(detail.Exercise.Answers).AnswerText);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Get, route, bearer: stranger.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.SendAsync(HttpMethod.Get, route, bearer: reader.Token)).StatusCode);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await reviewer.SendAsync(HttpMethod.Get, route, bearer: reader.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.SendAsync(HttpMethod.Get, route, bearer: german.Token)).StatusCode);
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.Version, "ChangesRequested")).EnsureSuccessStatusCode();
        using var deleted = await author.SendAsync(HttpMethod.Delete,
            $"/api/v1/manage/lessons/{lesson.Id}/exercises/{id}", new { versionId = lesson.Version }, writer.Token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal("", await deleted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await author.SendAsync(HttpMethod.Get, route, bearer: writer.Token)).StatusCode);
    }

    private sealed record ExerciseResourceId(Guid Id);
}
