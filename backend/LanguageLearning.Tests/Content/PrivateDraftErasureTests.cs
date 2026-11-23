using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Core.Learning;
using LanguageLearning.Tests.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Fact]
    public async Task PrivateLessonHandoverRejectsWrongLanguageAndPublishedHistory()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var foreignAuthor = Browser();
        var otherLanguage = await StaffAsync(foreignAuthor, RoleNames.ContentAuthor, 4);
        var draft = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        var request = new TransferLessonOwner(otherLanguage.Account.Email, writer.Account.Id,
            "Reviewed transfer request.");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Post,
            $"/api/v1/manage/lessons/{draft.Id}/owner", request, administrator.Token)).StatusCode);
        var published = await LearningTestContent.CreateAsync(fixture, writer.Account.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Post,
            $"/api/v1/manage/lessons/{published.LessonId}/owner",
            request with { Email = administrator.Account.Email }, administrator.Token)).StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(writer.Account.Id, await database.Lessons.Where(item => item.Id == published.LessonId)
            .Select(item => item.OwnerId).SingleAsync());
    }

    [Fact]
    public async Task OtherStaffDraftsOnAPrivateLessonRequireResolutionBeforeOwnerDeletion()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var draft = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        string root = $"/api/v1/manage/lessons/{draft.Id}";
        (await author.SendAsync(HttpMethod.Delete, root,
            new { versionId = draft.Version, reason = "Archived private work." }, writer.Token)).EnsureSuccessStatusCode();
        (await admin.SendAsync(HttpMethod.Post, root + "/versions", bearer: administrator.Token)).EnsureSuccessStatusCode();
        (await author.SendAsync(HttpMethod.Delete, $"{root}/versions/{draft.Version}/private-text", bearer: writer.Token)).EnsureSuccessStatusCode();
        var deletion = await author.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password, confirmation = "DELETE",
            recoveryCode = writer.Account.RecoveryCodes[1]
        }, writer.Token, csrf: true);
        Assert.Equal(HttpStatusCode.Conflict, deletion.StatusCode);
        Assert.Contains("unpublished content", await deletion.Content.ReadAsStringAsync());
        var handover = new TransferLessonOwner(administrator.Account.Email, writer.Account.Id,
            "Reviewed shared draft and retained the other staff contribution.");
        Assert.Equal(HttpStatusCode.Forbidden, (await author.SendAsync(HttpMethod.Post,
            root + "/owner", handover, writer.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Post,
            root + "/owner", handover with { ExpectedOwnerId = Guid.NewGuid() }, administrator.Token)).StatusCode);
        (await admin.SendAsync(HttpMethod.Post, root + "/owner", handover, administrator.Token)).EnsureSuccessStatusCode();
        (await admin.SendAsync(HttpMethod.Post, root + "/owner", handover, administrator.Token)).EnsureSuccessStatusCode();
        var removed = await author.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password, confirmation = "DELETE",
            recoveryCode = writer.Account.RecoveryCodes[1]
        }, writer.Token, csrf: true);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var final = finalScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(administrator.Account.Id, await final.Lessons.Where(item => item.Id == draft.Id)
            .Select(item => item.OwnerId).SingleAsync());
        Assert.True(await final.LessonVersions.AnyAsync(item => item.LessonId == draft.Id
            && item.CreatedBy == administrator.Account.Id && item.PrivateTextErasedAt == null));
        Assert.Equal(1, await final.AuditLogs.CountAsync(item => item.EntityId == draft.Id.ToString()
            && item.Action == "PrivateLessonOwnershipTransferred"));
    }

    [Fact]
    public async Task LegacyVersionWithLearningHistoryCannotBeErasedEvenWithoutPublicationMetadata()
    {
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var content = await LearningTestContent.CreateAsync(fixture, writer.Account.Id);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await database.Lessons.Where(item => item.Id == content.LessonId).ExecuteUpdateAsync(update =>
            update.SetProperty(item => item.CurrentVersionId, (Guid?)null).SetProperty(item => item.Status, "Archived"));
        await database.LessonVersions.Where(item => item.Id == content.VersionId).ExecuteUpdateAsync(update =>
            update.SetProperty(item => item.PublishedAt, (DateTimeOffset?)null).SetProperty(item => item.Status, "Archived"));
        database.Enrollments.Add(new Enrollment { UserId = writer.Account.Id, CourseId = content.CourseId, EnrolledAt = DateTimeOffset.UtcNow });
        database.LearningSessions.Add(new LearningSession
        {
            UserId = writer.Account.Id, CourseId = content.CourseId, LessonId = content.LessonId,
            LessonVersionId = content.VersionId, Title = "Historical lesson", Explanation = "",
            StartedAt = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync();
        var response = await author.SendAsync(HttpMethod.Delete,
            $"/api/v1/manage/lessons/{content.LessonId}/versions/{content.VersionId}/private-text", bearer: writer.Token);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(await database.Exercises.AnyAsync(item => item.LessonVersionId == content.VersionId));
        Assert.Null(await database.LessonVersions.Where(item => item.Id == content.VersionId).Select(item => item.PrivateTextErasedAt).SingleAsync());
    }

    [Fact]
    public async Task ConcurrentPublicationAndErasureHaveOneWinner()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser();
        var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var draft = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        (await SubmitAsync(author, writer.Token, draft.Id, draft.Version)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, draft.Id, draft.Version)).EnsureSuccessStatusCode();
        var outcomes = await Task.WhenAll(
            PublishAsync(admin, administrator.Token, draft.Id, draft.Version),
            author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/lessons/{draft.Id}/versions/{draft.Version}/private-text", bearer: writer.Token));
        Assert.Single(outcomes, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(outcomes, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PrivateDraftErasureRequiresOwnershipAndUnblocksConfirmedAccountDeletion()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var stranger = Browser();
        var foreign = await StaffAsync(stranger, RoleNames.ContentAuthor);
        var draft = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        string erase = $"/api/v1/manage/lessons/{draft.Id}/versions/{draft.Version}/private-text";
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.SendAsync(HttpMethod.Delete, erase, bearer: foreign.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await author.SendAsync(HttpMethod.Delete, erase, bearer: writer.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await author.SendAsync(HttpMethod.Delete, erase, bearer: writer.Token)).StatusCode);
        var view = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{draft.Id}", bearer: writer.Token))
            .Content.ReadFromJsonAsync<StaffLesson>())!;
        Assert.NotNull(view.PrivateTextErasedAt);
        Assert.Equal("Archived", view.VersionStatus);
        Assert.Empty(view.Exercises);
        Assert.Equal("", view.Explanation);
        var deletion = await author.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password, confirmation = "DELETE",
            recoveryCode = writer.Account.RecoveryCodes[1]
        }, writer.Token, csrf: true);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.False(await database.Exercises.AnyAsync(item => item.LessonVersionId == draft.Version));
        Assert.Equal("Erased private draft", (await database.Lessons.SingleAsync(item => item.Id == draft.Id)).Title);
        Assert.Equal(1, await database.AuditLogs.CountAsync(item => item.EntityId == draft.Id.ToString() && item.Action == "LessonPrivateTextErased"));
    }

    [Fact]
    public async Task ErasingAnUnpublishedRevisionPreservesPublishedExercisesAndCopiesThemForTheNextDraft()
    {
        using var admin = Browser();
        var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser();
        var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser();
        var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var draft = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        (await SubmitAsync(author, writer.Token, draft.Id, draft.Version)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, draft.Id, draft.Version)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, draft.Id, draft.Version)).EnsureSuccessStatusCode();
        string root = $"/api/v1/manage/lessons/{draft.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Delete,
            $"{root}/versions/{draft.Version}/private-text", bearer: administrator.Token)).StatusCode);
        (await author.SendAsync(HttpMethod.Post, root + "/versions", bearer: writer.Token)).EnsureSuccessStatusCode();
        var revision = (await (await author.SendAsync(HttpMethod.Get, root, bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        (await author.SendAsync(HttpMethod.Delete, $"{root}/versions/{revision.VersionId}/private-text", bearer: writer.Token)).EnsureSuccessStatusCode();
        var published = (await (await author.SendAsync(HttpMethod.Get, $"{root}?versionId={draft.Version}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        Assert.NotNull(published.VersionPublishedAt);
        Assert.False(published.IsLatestVersion);
        Assert.NotEmpty(published.Exercises);
        (await author.SendAsync(HttpMethod.Post, root + "/versions", bearer: writer.Token)).EnsureSuccessStatusCode();
        var next = (await (await author.SendAsync(HttpMethod.Get, root, bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        Assert.Equal(revision.VersionNumber + 1, next.VersionNumber);
        Assert.Equal(published.Title, next.Title);
        Assert.Equal(published.Exercises.Count, next.Exercises.Count);
        Assert.Contains(next.Exercises[0].Answers, item => item.AnswerText == published.Exercises[0].Answers[0].AnswerText);
        Assert.Null(next.PrivateTextErasedAt);
    }
}
