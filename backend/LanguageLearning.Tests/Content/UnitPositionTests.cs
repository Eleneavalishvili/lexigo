using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task UnitSwapChangesPublicOrderAndKeepsLessonsInTheirUnits()
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
        foreach (var lesson in new[] { first, second })
        {
            (await SubmitAsync(author, writer.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
            (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
            (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.Version)).EnsureSuccessStatusCode();
        }

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var positions = await database.Units.Where(item => item.Id == firstUnit || item.Id == secondUnit)
            .ToDictionaryAsync(item => item.Id, item => item.OrderNumber);
        var move = new { fromPosition = positions[firstUnit], toPosition = positions[secondUnit] };
        string route = $"/api/v1/manage/units/{firstUnit}/position";
        using var denied = await author.SendAsync(HttpMethod.Put, route, move, writer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        (await admin.SendAsync(HttpMethod.Put, route, move, administrator.Token)).EnsureSuccessStatusCode();
        using var stale = await admin.SendAsync(HttpMethod.Put, route, move, administrator.Token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var units = await fixture.Client.GetFromJsonAsync<CatalogUnit[]>($"/api/v1/courses/{Spanish}/units");
        Assert.Equal(new[] { secondUnit, firstUnit }, units!
            .Where(item => item.Id == firstUnit || item.Id == secondUnit).Select(item => item.Id));
        var lessonAfter = await fixture.Client.GetFromJsonAsync<CatalogLesson>($"/api/v1/lessons/{first.Id}");
        Assert.Equal(firstUnit, lessonAfter!.UnitId);
        Assert.Equal(first.Version, lessonAfter.VersionId);
    }
}
