using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Learning;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task AdministratorCanSwapPublishedLessonsWithoutChangingSessionsOrPrerequisites()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser();
        var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var unit = await UnitAsync(admin, administrator.Token);
        var first = await DraftAsync(author, writer.Token, unit);
        var second = await DraftAsync(author, writer.Token, unit, 2);
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
        using var started = await author.SendAsync(HttpMethod.Post,
            $"/api/v1/lessons/{first.Id}/sessions", bearer: writer.Token);
        started.EnsureSuccessStatusCode();
        var session = (await started.Content.ReadFromJsonAsync<SessionView>())!;
        string route = $"/api/v1/manage/lessons/{first.Id}/position";
        var move = new { fromPosition = 1, toPosition = 2 };
        using var denied = await author.SendAsync(HttpMethod.Put, route, move, writer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        (await admin.SendAsync(HttpMethod.Put, route, move, administrator.Token)).EnsureSuccessStatusCode();
        using var stale = await admin.SendAsync(HttpMethod.Put, route, move, administrator.Token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var list = await fixture.Client.GetFromJsonAsync<CatalogLesson[]>($"/api/v1/units/{unit}/lessons");
        Assert.Equal(new[] { second.Id, first.Id }, list!.Select(item => item.Id));
        Assert.Equal(new[] { second.Version, first.Version }, list!.Select(item => item.VersionId));
        using var reopened = await author.SendAsync(HttpMethod.Get,
            $"/api/v1/me/learning-sessions/{session.Id}", bearer: writer.Token);
        reopened.EnsureSuccessStatusCode();
        using var locked = await author.SendAsync(HttpMethod.Post,
            $"/api/v1/lessons/{second.Id}/sessions", bearer: writer.Token);
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);

        (await admin.SendAsync(HttpMethod.Put, route, new { fromPosition = 2, toPosition = 7 },
            administrator.Token)).EnsureSuccessStatusCode();
        var moved = await fixture.Client.GetFromJsonAsync<CatalogLesson>($"/api/v1/lessons/{first.Id}");
        Assert.Equal(7, moved!.OrderNumber);
    }
}
