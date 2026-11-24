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
    public async Task CourseCreationRequiresAdminAndKeepsTheNewPairPrivate()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var input = new CreateCourseRequest(2, 3, "Italian through Spanish", "An unpublished curriculum.");
        using var forbidden = await author.SendAsync(HttpMethod.Post, "/api/v1/manage/courses", input, writer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var created = await admin.SendAsync(HttpMethod.Post, "/api/v1/manage/courses", input, administrator.Token);
        var id = await IdAsync(created, HttpStatusCode.Created, "/api/v1/manage/courses");
        using var details = await admin.SendAsync(HttpMethod.Get, created.Headers.Location!.PathAndQuery, bearer: administrator.Token);
        var course = await details.Content.ReadFromJsonAsync<StaffCourseDetail>();
        Assert.Equal(id, course!.Id);
        Assert.False(course.IsPublished);
        using var outOfScope = await author.SendAsync(HttpMethod.Get, created.Headers.Location.PathAndQuery, bearer: writer.Token);
        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);
        using var duplicate = await admin.SendAsync(HttpMethod.Post, "/api/v1/manage/courses", input, administrator.Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await database.AuditLogs.CountAsync(row => row.EntityId == id.ToString() && row.Action == "CourseCreated"));
    }

    [Fact]
    public async Task ConcurrentCourseCreationDoesNotDuplicatePairOrAudit()
    {
        using var a = Browser();
        using var b = Browser();
        var first = await StaffAsync(a, RoleNames.Admin);
        var second = await StaffAsync(b, RoleNames.Admin);
        var input = new CreateCourseRequest(3, 4, "German through Italian", "Draft content only.");
        var results = await Task.WhenAll(
            a.SendAsync(HttpMethod.Post, "/api/v1/manage/courses", input, first.Token),
            b.SendAsync(HttpMethod.Post, "/api/v1/manage/courses", input, second.Token));
        try
        {
            Assert.Single(results, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in results) response.Dispose(); }
    }

    [Fact]
    public async Task CourseCreationRejectsUnknownIdenticalAndExistingLanguages()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        foreach (var pair in new[] { (1, 1), (1, 999999), (1, 2) })
        {
            using var response = await admin.SendAsync(HttpMethod.Post, "/api/v1/manage/courses",
                new CreateCourseRequest(pair.Item1, pair.Item2, "New course", "Valid description"), administrator.Token);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        using var malformed = await admin.SendAsync(HttpMethod.Post, "/api/v1/manage/courses",
            new CreateCourseRequest(2, 4, "", "Valid description"), administrator.Token);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }
}
