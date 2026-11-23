using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Learning;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task CourseArchiveHidesPublishedLessonsAndPreservesExistingSessions()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser();
        var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var unit = await UnitAsync(admin, administrator.Token);
        var lesson = await DraftAsync(author, writer.Token, unit);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
        (await author.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = Spanish }, writer.Token))
            .EnsureSuccessStatusCode();
        string startRoute = $"/api/v1/lessons/{lesson.Id}/sessions";
        using var started = await author.SendAsync(HttpMethod.Post, startRoute, bearer: writer.Token);
        started.EnsureSuccessStatusCode();
        var session = (await started.Content.ReadFromJsonAsync<SessionView>())!;

        string archiveRoute = $"/api/v1/manage/courses/{Spanish}/archive";
        var archive = new { archived = true, reason = "Temporary curriculum review" };
        using var forbidden = await author.SendAsync(HttpMethod.Put, archiveRoute, archive, writer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var missingState = await admin.SendAsync(HttpMethod.Put, archiveRoute,
            new { reason = archive.reason }, administrator.Token);
        Assert.Equal(HttpStatusCode.BadRequest, missingState.StatusCode);
        (await admin.SendAsync(HttpMethod.Put, archiveRoute, archive, administrator.Token)).EnsureSuccessStatusCode();
        using var hidden = await fixture.Client.GetAsync($"/api/v1/lessons/{lesson.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var blocked = await author.SendAsync(HttpMethod.Post, startRoute, bearer: writer.Token);
        Assert.Equal(HttpStatusCode.NotFound, blocked.StatusCode);
        using var reopened = await author.SendAsync(HttpMethod.Get,
            $"/api/v1/me/learning-sessions/{session.Id}", bearer: writer.Token);
        reopened.EnsureSuccessStatusCode();
        using var republish = await PublishAsync(admin, administrator.Token, lesson.Id, lesson.Version);
        Assert.Equal(HttpStatusCode.Conflict, republish.StatusCode);

        var restore = new { archived = false, reason = "Curriculum review complete" };
        (await admin.SendAsync(HttpMethod.Put, archiveRoute, restore, administrator.Token)).EnsureSuccessStatusCode();
        using var restored = await fixture.Client.GetAsync($"/api/v1/lessons/{lesson.Id}");
        restored.EnsureSuccessStatusCode();
    }
}
