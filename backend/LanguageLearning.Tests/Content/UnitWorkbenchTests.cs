using System.Net;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task OnlyAdministratorsCanEditUnitsAndOccupiedPositionsAreRejected()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid first = await UnitAsync(admin, administrator.Token);
        Guid second = await UnitAsync(admin, administrator.Token);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        int occupied = await database.Units.Where(item => item.Id == second)
            .Select(item => item.OrderNumber).SingleAsync();
        object Edit(int position) => new
        {
            title = "Everyday conversations",
            description = "Practise introductions and common questions.",
            difficulty = "A2",
            orderNumber = position
        };

        var route = $"/api/v1/manage/units/{first}";
        using var unauthorized = await author.SendAsync(HttpMethod.Put, route, Edit(occupied + 1), writer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);
        using var collision = await admin.SendAsync(HttpMethod.Put, route, Edit(occupied), administrator.Token);
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        using var success = await admin.SendAsync(HttpMethod.Put, route, Edit(occupied + 1), administrator.Token);
        success.EnsureSuccessStatusCode();

        var edited = await database.Units.AsNoTracking().SingleAsync(item => item.Id == first);
        Assert.Equal("Everyday conversations", edited.Title);
        Assert.Equal("Practise introductions and common questions.", edited.Description);
        Assert.Equal("A2", edited.Difficulty);
        Assert.Equal(occupied + 1, edited.OrderNumber);
        Assert.Equal(Spanish, edited.CourseId);
        Assert.True(await database.AuditLogs.AnyAsync(item => item.Action == "UnitEdited"
            && item.EntityId == first.ToString() && item.ActorId == administrator.Account.Id));
    }
}
