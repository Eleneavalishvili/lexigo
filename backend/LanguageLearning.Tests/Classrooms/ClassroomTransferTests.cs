using System.Net;
using LanguageLearning.Infrastructure.Classrooms;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Classrooms;

public sealed partial class ClassroomTests
{
    [Fact]
    public async Task OnlyTheDesignatedTeacherCanAcceptAndRepeatedAcceptancePreservesClassHistory()
    {
        using var original = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var receiving = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var outsider = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(original, "Teacher");
        var replacement = await Account(receiving, "Teacher");
        var learner = await Account(outsider);
        var classroom = await Create(original, teacher.Token, teacher.User.Id);
        string path = $"/api/v1/classes/{classroom.Id}";
        var invitation = await Invite(original, teacher.Token, classroom.Id);
        var pending = await Read<DeletionResponsibility[]>(await original.SendAsync(HttpMethod.Get,
            "/api/v1/me/deletion-responsibilities", bearer: teacher.Token));
        Assert.Contains(pending, item => item.Code == "classes");
        (await Join(outsider, learner.Token, invitation.Code)).EnsureSuccessStatusCode();
        using var offerResponse = await original.SendAsync(HttpMethod.Post, path + "/transfer",
            new { email = replacement.User.Email }, teacher.Token);
        var offer = await Read<ClassTransferLink>(offerResponse);
        Assert.True(offerResponse.Headers.CacheControl!.NoStore);
        var payload = new { token = offer.Token };
        Assert.Equal(HttpStatusCode.Unauthorized, (await outsider.SendAsync(HttpMethod.Post,
            "/api/v1/classes/transfers/accept", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Post,
            "/api/v1/classes/transfers/accept", payload, learner.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await receiving.SendAsync(HttpMethod.Post,
            "/api/v1/classes/transfers/preview", new { token = "tampered" }, replacement.Token)).StatusCode);
        (await receiving.SendAsync(HttpMethod.Post, "/api/v1/classes/transfers/preview", payload,
            replacement.Token)).EnsureSuccessStatusCode();
        var accepted = await Task.WhenAll(
            receiving.SendAsync(HttpMethod.Post, "/api/v1/classes/transfers/accept", payload, replacement.Token),
            receiving.SendAsync(HttpMethod.Post, "/api/v1/classes/transfers/accept", payload, replacement.Token));
        Assert.All(accepted, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var detail = await Read<ClassroomDetail>(await receiving.SendAsync(HttpMethod.Get, path, bearer: replacement.Token));
        Assert.True(detail.Classroom.IsTeacher);
        Assert.Contains(detail.Members!.Items, item => item.UserId == learner.User.Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await original.SendAsync(HttpMethod.Get, path, bearer: teacher.Token)).StatusCode);
        var afterHandover = await Read<DeletionResponsibility[]>(await original.SendAsync(HttpMethod.Get,
            "/api/v1/me/deletion-responsibilities", bearer: teacher.Token));
        Assert.DoesNotContain(afterHandover, item => item.Code == "classes");
        Assert.Equal(HttpStatusCode.OK,
            (await outsider.SendAsync(HttpMethod.Post, "/api/v1/classes/join", new { code = invitation.Code }, learner.Token)).StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.All(await db.ClassInvitations.Where(item => item.ClassroomId == classroom.Id).ToArrayAsync(),
            item => Assert.NotNull(item.RevokedAt));
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.EntityId == classroom.Id.ToString()
            && item.Action == "ClassroomTransferred"));
    }

    [Fact]
    public async Task SavingClassChangesInvalidatesAnOutstandingHandover()
    {
        using var original = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var receiving = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(original, "Teacher");
        var replacement = await Account(receiving, "Teacher");
        var classroom = await Create(original, teacher.Token, teacher.User.Id);
        string path = $"/api/v1/classes/{classroom.Id}";
        var offer = await Read<ClassTransferLink>(await original.SendAsync(HttpMethod.Post, path + "/transfer",
            new { email = replacement.User.Email }, teacher.Token));
        var detail = await Read<ClassroomDetail>(await original.SendAsync(HttpMethod.Get, path, bearer: teacher.Token));
        (await original.SendAsync(HttpMethod.Patch, path, new EditClassroomInput(
            "Updated class", "", false, detail.Classroom.Revision), teacher.Token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await receiving.SendAsync(HttpMethod.Post,
            "/api/v1/classes/transfers/preview", new { token = offer.Token }, replacement.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await receiving.SendAsync(HttpMethod.Post,
            "/api/v1/classes/transfers/accept", new { token = offer.Token }, replacement.Token)).StatusCode);
    }
}
