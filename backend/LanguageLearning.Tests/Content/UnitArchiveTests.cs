using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Learning;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task UnitArchivePreservesSessionsAndRequiresDependencyOrder()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser();
        var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var firstUnit = await UnitAsync(admin, administrator.Token);
        var secondUnit = await UnitAsync(admin, administrator.Token);
        var first = await DraftAsync(author, writer.Token, firstUnit);
        var second = await DraftAsync(author, writer.Token, secondUnit);
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/lessons/{second.Id}/prerequisites",
            new { lessonIds = new[] { first.Id } }, writer.Token)).EnsureSuccessStatusCode();
        foreach (var lesson in new[] { first, second })
        {
            (await SubmitAsync(author, writer.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
            (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
            (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
        }
        (await author.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = Spanish }, writer.Token))
            .EnsureSuccessStatusCode();
        string startRoute = $"/api/v1/lessons/{first.Id}/sessions";
        using var started = await author.SendAsync(HttpMethod.Post, startRoute, bearer: writer.Token);
        started.EnsureSuccessStatusCode();
        var session = (await started.Content.ReadFromJsonAsync<SessionView>())!;

        Task<HttpResponseMessage> SetArchive(Guid unitId, bool archived) =>
            admin.SendAsync(HttpMethod.Put, $"/api/v1/manage/units/{unitId}/archive",
                new { archived, reason = "Curriculum review" }, administrator.Token);
        using var forbidden = await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/units/{firstUnit}/archive",
            new { archived = true, reason = "Curriculum review" }, writer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var blockedArchive = await SetArchive(firstUnit, true);
        Assert.Equal(HttpStatusCode.Conflict, blockedArchive.StatusCode);
        (await SetArchive(secondUnit, true)).EnsureSuccessStatusCode();
        using var visibleFirst = await fixture.Client.GetAsync($"/api/v1/lessons/{first.Id}");
        visibleFirst.EnsureSuccessStatusCode();
        using var hiddenSecond = await fixture.Client.GetAsync($"/api/v1/units/{secondUnit}/lessons");
        Assert.Equal(HttpStatusCode.NotFound, hiddenSecond.StatusCode);
        (await SetArchive(firstUnit, true)).EnsureSuccessStatusCode();
        using var blockedStart = await author.SendAsync(HttpMethod.Post, startRoute, bearer: writer.Token);
        Assert.Equal(HttpStatusCode.NotFound, blockedStart.StatusCode);
        using var reopened = await author.SendAsync(HttpMethod.Get,
            $"/api/v1/me/learning-sessions/{session.Id}", bearer: writer.Token);
        reopened.EnsureSuccessStatusCode();
        using var blockedRestore = await SetArchive(secondUnit, false);
        Assert.Equal(HttpStatusCode.Conflict, blockedRestore.StatusCode);
        (await SetArchive(firstUnit, false)).EnsureSuccessStatusCode();
        (await SetArchive(secondUnit, false)).EnsureSuccessStatusCode();
        using var restored = await fixture.Client.GetAsync($"/api/v1/lessons/{second.Id}");
        restored.EnsureSuccessStatusCode();
    }
}
