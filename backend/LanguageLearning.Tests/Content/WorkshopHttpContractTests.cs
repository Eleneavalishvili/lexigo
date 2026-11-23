using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task CreatedUnitHasAStaffDetailUrlWithCourseLanguageScope()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        Guid unitId = await UnitAsync(admin, administrator.Token);
        string path = $"/api/v1/manage/units/{unitId}";
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var otherLanguage = Browser();
        var other = await StaffAsync(otherLanguage, RoleNames.ContentAuthor, language: 4);

        using var detail = await author.SendAsync(HttpMethod.Get, path, bearer: writer.Token);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var unit = await detail.Content.ReadFromJsonAsync<StaffUnit>();
        Assert.NotNull(unit);
        Assert.Equal(unitId, unit.Id);
        Assert.Equal("First words", unit.Title);
        Assert.Equal(1, unit.NextLessonOrder);
        using var hidden = await otherLanguage.SendAsync(HttpMethod.Get, path, bearer: other.Token);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var anonymous = await fixture.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task WorkshopRoutesAreDocumentedAndMissingCatalogResourceReturnsProblemDetails()
    {
        using var schema = await fixture.Client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json");
        var paths = schema!.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/manage/units/{id}/position").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/manage/courses/{id}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/manage/lessons/{lessonId}/versions/{versionId}/exercises/{exerciseId}/preview-speech")
            .TryGetProperty("post", out _));
        using var response = await fixture.Client.GetAsync($"/api/v1/lessons/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(404, problem!.RootElement.GetProperty("status").GetInt32());
    }
}
