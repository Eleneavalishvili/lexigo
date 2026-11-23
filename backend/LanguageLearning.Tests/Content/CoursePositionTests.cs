using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task CoursePositionSwapsCatalogOrderAndRejectsAStaleMove()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var germanAuthor = Browser();
        var germanWriter = await StaffAsync(germanAuthor, RoleNames.ContentAuthor, 4);
        using var reviewer = Browser();
        var spanishReview = await StaffAsync(reviewer, RoleNames.Reviewer);
        using var germanReviewer = Browser();
        var germanReview = await StaffAsync(germanReviewer, RoleNames.Reviewer, 4);
        var spanish = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        var german = await DraftAsync(germanAuthor, germanWriter.Token, await UnitAsync(admin, administrator.Token, German));
        (await SubmitAsync(author, writer.Token, spanish.Id, spanish.Version)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, spanishReview.Token, spanish.Id, spanish.Version)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, spanish.Id, spanish.Version)).EnsureSuccessStatusCode();
        (await SubmitAsync(germanAuthor, germanWriter.Token, german.Id, german.Version)).EnsureSuccessStatusCode();
        (await ReviewAsync(germanReviewer, germanReview.Token, german.Id, german.Version)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, german.Id, german.Version)).EnsureSuccessStatusCode();

        string route = $"/api/v1/manage/courses/{Spanish}/position";
        var move = new { fromPosition = 1, toPosition = 3 };
        using var denied = await author.SendAsync(HttpMethod.Put, route, move, writer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        (await admin.SendAsync(HttpMethod.Put, route, move, administrator.Token)).EnsureSuccessStatusCode();
        using var stale = await admin.SendAsync(HttpMethod.Put, route, move, administrator.Token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var catalog = await fixture.Client.GetFromJsonAsync<CatalogCourse[]>("/api/v1/courses");
        Assert.Equal(new[] { German, Spanish }, catalog!.Where(item => item.Id == German || item.Id == Spanish).Select(item => item.Id));
        Assert.Equal(spanish.Version, (await fixture.Client.GetFromJsonAsync<CatalogLesson>($"/api/v1/lessons/{spanish.Id}"))!.VersionId);
        var editRoute = $"/api/v1/manage/courses/{Spanish}";
        var details = new { title = "Spanish practice", description = "Spanish through everyday conversations." };
        using var deniedEdit = await author.SendAsync(HttpMethod.Put, editRoute, details, writer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, deniedEdit.StatusCode);
        (await admin.SendAsync(HttpMethod.Put, editRoute, details, administrator.Token)).EnsureSuccessStatusCode();
        var edited = await fixture.Client.GetFromJsonAsync<CatalogCourse>($"/api/v1/courses/{Spanish}");
        Assert.Equal(details.title, edited!.Title);
        Assert.Equal(details.description, edited.Description);
        var original = catalog!.Single(item => item.Id == Spanish);
        (await admin.SendAsync(HttpMethod.Put, editRoute, new { original.Title, original.Description },
            administrator.Token)).EnsureSuccessStatusCode();
        (await admin.SendAsync(HttpMethod.Put, route, new { fromPosition = 3, toPosition = 1 },
            administrator.Token)).EnsureSuccessStatusCode();
    }
}
