using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Tests.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private static readonly Guid Spanish = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid German = Guid.Parse("10000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task DraftExercisesCanBeCorrectedAndRemovedButNotAfterSubmission()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var lesson = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        var draft = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        Guid exercise = draft.Exercises[0].Id;
        var correction = new { versionId = lesson.Version, type = "MultipleChoice", prompt = "Corrected prompt", explanation = "English feedback", orderNumber = 1, options = new[] { "Buenos días", "Buenas noches" }, answers = new[] { "Buenos días" }, ignoreCase = false, ignoreTerminalPunctuation = true };
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/lessons/{lesson.Id}/exercises/{exercise}", correction, writer.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        var corrected = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        corrected.Exercises[0].Answers.Should().ContainSingle(item => item.AnswerText == "Buenos días");
        corrected.Exercises[0].EvaluationRules.Should().Contain("false").And.Contain("true");
        await SubmitAsync(author, writer.Token, lesson.Id, lesson.Version);
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/lessons/{lesson.Id}/exercises/{exercise}", correction, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/lessons/{lesson.Id}/exercises/{exercise}", new { versionId = lesson.Version }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.Version, "ChangesRequested");
        (await author.SendAsync(HttpMethod.Delete, $"/api/v1/manage/lessons/{lesson.Id}/exercises/{exercise}", new { versionId = lesson.Version }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var scope = fixture.Services.CreateAsyncScope(); var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.AcceptedAnswers.CountAsync(item => item.ExerciseId == exercise)).Should().Be(0);
        (await database.ExerciseOptions.CountAsync(item => item.ExerciseId == exercise)).Should().Be(0);
    }

    [Fact]
    public async Task ReviewQueueIsScopedAndConcurrentApprovalOccursOnce()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var lesson = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        (await reviewer.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: reader.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SubmitAsync(author, writer.Token, lesson.Id, lesson.Version);
        (await reviewer.SendAsync(HttpMethod.Get, $"/api/v1/manage/courses", bearer: reader.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await reviewer.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons?courseId={Spanish}", bearer: reader.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        var details = (await (await reviewer.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: reader.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        details.Exercises[0].Answers.Should().ContainSingle(item => item.AnswerText == "Hola");
        var results = await Task.WhenAll(ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.Version), ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.Version));
        results.Count(result => result.StatusCode == HttpStatusCode.OK).Should().Be(1);
        (await reviewer.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}/versions", bearer: reader.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostgreSqlRejectsCrossLessonCurrentVersionsAndSelfPrerequisites()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid unit = await UnitAsync(admin, administrator.Token); var a = await DraftAsync(author, writer.Token, unit); var b = await DraftAsync(author, writer.Token, unit, 2);
        await using var scope = fixture.Services.CreateAsyncScope(); var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await FluentActions.Invoking(() => database.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Lessons\" SET \"CurrentVersionId\"={b.Version} WHERE \"Id\"={a.Id}"))
            .Should().ThrowAsync<PostgresException>().Where(error => error.SqlState == PostgresErrorCodes.ForeignKeyViolation);
        await FluentActions.Invoking(() => database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"LessonPrerequisites\" (\"LessonId\",\"PrerequisiteLessonId\") VALUES ({a.Id},{a.Id})"))
            .Should().ThrowAsync<PostgresException>().Where(error => error.SqlState == PostgresErrorCodes.CheckViolation);
    }
    private SessionTestBrowser Browser() => new(fixture.CreateIsolatedClient());
    private async Task<(SecurityTestAccount Account, string Token)> StaffAsync(SessionTestBrowser browser, string role, int language = 2)
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await users.AddToRoleAsync((await users.FindByIdAsync(account.Id.ToString()))!, role);
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        database.UserLanguageScopes.Add(new UserLanguageScope { UserId = account.Id, LanguageId = language });
        await database.SaveChangesAsync();
        return (account, await account.LoginAsync(browser));
    }
    private async Task<Guid> UnitAsync(SessionTestBrowser admin, string token, Guid? courseId = null)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid course = courseId ?? Spanish;
        int order = (await database.Units.Where(item => item.CourseId == course).Select(item => (int?)item.OrderNumber).MaxAsync() ?? 0) + 1;
        var response = await admin.SendAsync(HttpMethod.Post, "/api/v1/manage/units", new { courseId = course, title = "First words", description = "Everyday greetings", orderNumber = order, difficulty = "Beginner" }, token);
        return await IdAsync(response, HttpStatusCode.Created, "/api/v1/manage/units");
    }
    private static async Task<Guid> IdAsync(HttpResponseMessage response, HttpStatusCode expectedStatus = HttpStatusCode.OK,
        string locationBase = "/api/v1/manage/lessons")
    {
        response.StatusCode.Should().Be(expectedStatus);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        if (expectedStatus == HttpStatusCode.Created)
        {
            var location = response.Headers.Location;
            Assert.NotNull(location);
            location.AbsolutePath.Should().Be($"{locationBase}/{id}");
        }
        return id;
    }

    private static async Task<Guid> RevisionAsync(SessionTestBrowser browser, string token, Guid lessonId)
    {
        using var response = await browser.SendAsync(HttpMethod.Post,
            $"/api/v1/manage/lessons/{lessonId}/versions", bearer: token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid versionId = body.RootElement.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/manage/lessons/{lessonId}", response.Headers.Location!.AbsolutePath);
        Assert.Contains($"versionId={versionId}", response.Headers.Location.Query);
        using var detail = await browser.SendAsync(HttpMethod.Get,
            response.Headers.Location.PathAndQuery, bearer: token);
        detail.EnsureSuccessStatusCode();
        Assert.Equal(versionId, (await detail.Content.ReadFromJsonAsync<StaffLesson>())!.VersionId);
        return versionId;
    }
    private async Task<(Guid Id, Guid Version)> DraftAsync(SessionTestBrowser author, string token, Guid unitId, int order = 1, string type = "MultipleChoice")
    {
        Guid id = await IdAsync(await author.SendAsync(HttpMethod.Post, "/api/v1/manage/lessons", new { unitId, title = "Say hello", objective = "Recognize a greeting", explanation = "An English grammar explanation.", orderNumber = order }, token), HttpStatusCode.Created);
        var details = await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{id}", bearer: token)).Content.ReadFromJsonAsync<StaffLesson>();
        var exercise = await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{id}/exercises", new { versionId = details!.VersionId, type, prompt = "Choose the greeting", explanation = "A greeting starts a conversation.", orderNumber = 1, options = type == "MultipleChoice" ? new[] { "Hola", "Adiós" } : Array.Empty<string>(), answers = type == "Speaking" ? Array.Empty<string>() : new[] { "Hola" } }, token);
        exercise.StatusCode.Should().Be(HttpStatusCode.Created);
        return (id, details.VersionId);
    }
    private Task<HttpResponseMessage> SubmitAsync(SessionTestBrowser browser, string token, Guid id, Guid version) => browser.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{id}/submit", new { versionId = version }, token);
    private Task<HttpResponseMessage> ReviewAsync(SessionTestBrowser browser, string token, Guid id, Guid version, string decision = "Approved") => browser.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{id}/reviews", new { versionId = version, decision, comment = "Reviewed the language and answer choices." }, token);
    private Task<HttpResponseMessage> PublishAsync(SessionTestBrowser browser, string token, Guid id, Guid version) => browser.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{id}/publish", new { versionId = version }, token);

    [Fact]
    public async Task IndependentReviewPublishesWithoutExposingKeysAndRevisionPreservesOldVersion()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        Guid unit = await UnitAsync(admin, administrator.Token); var lesson = await DraftAsync(author, writer.Token, unit);
        var staffRoute = $"/api/v1/manage/lessons/{lesson.Id}";
        var draft = (await (await author.SendAsync(HttpMethod.Get, staffRoute, bearer: writer.Token))
            .Content.ReadFromJsonAsync<StaffLesson>())!;
        var grammar = draft.Skills!.Single(skill => skill.Name == "Grammar");
        var exerciseRoute = $"{staffRoute}/exercises/{draft.Exercises.Single().Id}";
        var update = new { versionId = lesson.Version, type = "MultipleChoice", prompt = "Choose the greeting",
            explanation = "A greeting starts a conversation.", orderNumber = 1,
            options = new[] { "Hola", "Adiós" }, answers = new[] { "Hola" }, skillIds = new[] { grammar.Id } };
        (await author.SendAsync(HttpMethod.Put, exerciseRoute, update with { skillIds = new[] { int.MaxValue } }, writer.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await author.SendAsync(HttpMethod.Put, exerciseRoute, update, writer.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fixture.Client.GetAsync($"/api/v1/lessons/{lesson.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.OK);
        string json = await (await fixture.Client.GetAsync($"/api/v1/lessons/{lesson.Id}")).Content.ReadAsStringAsync();
        json.Should().Contain("Say hello").And.NotContain("evaluationRules").And.NotContain("acceptedAnswer").And.NotContain("matchKey").And.NotContain("Hola");
        (await fixture.Client.GetAsync($"/api/v1/courses/{Spanish}/units")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fixture.Client.GetAsync($"/api/v1/units/{unit}/lessons")).StatusCode.Should().Be(HttpStatusCode.OK);
        Guid revision = await RevisionAsync(author, writer.Token, lesson.Id);
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/lessons/{lesson.Id}", new { versionId = revision, title = "Private new title", objective = "New objective", explanation = "Unpublished private grammar" }, writer.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        string publicJson = await (await fixture.Client.GetAsync($"/api/v1/lessons/{lesson.Id}")).Content.ReadAsStringAsync();
        publicJson.Should().Contain("Say hello").And.NotContain("Private new title").And.NotContain("Unpublished private grammar");
        await using var scope = fixture.Services.CreateAsyncScope(); var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.Exercises.CountAsync(item => item.LessonVersionId == revision)).Should().Be(1);
        var revised = (await (await author.SendAsync(HttpMethod.Get, staffRoute, bearer: writer.Token))
            .Content.ReadFromJsonAsync<StaffLesson>())!;
        revised.Exercises.Single().SkillIds.Should().Equal(grammar.Id);
        (await database.LessonVersions.SingleAsync(item => item.Id == lesson.Version)).Status.Should().Be("Published");
        (await database.Exercises.SingleAsync(item => item.LessonVersionId == lesson.Version)).Prompt.Should().Be("Choose the greeting");
        (await admin.SendAsync(HttpMethod.Delete, $"/api/v1/manage/lessons/{lesson.Id}", new { versionId = revision, reason = "Archive this reviewed test lesson" }, administrator.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fixture.Client.GetAsync($"/api/v1/lessons/{lesson.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OwnershipScopesSelfReviewAndAuthorPublicationAreDenied()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid unit = await UnitAsync(admin, administrator.Token); var lesson = await DraftAsync(author, writer.Token, unit);
        using var another = Browser(); var other = await StaffAsync(another, RoleNames.ContentAuthor);
        (await another.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: other.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SubmitAsync(another, other.Token, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var wrongLanguage = Browser(); var foreign = await StaffAsync(wrongLanguage, RoleNames.Reviewer, 4);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReviewAsync(wrongLanguage, foreign.Token, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await PublishAsync(author, writer.Token, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using (var scope = fixture.Services.CreateAsyncScope()) { var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); await users.AddToRoleAsync((await users.FindByIdAsync(writer.Account.Id.ToString()))!, RoleNames.Reviewer); }
        string combined = await writer.Account.LoginAsync(author, 1);
        (await ReviewAsync(author, combined, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Guid germanUnit = await UnitAsync(admin, administrator.Token, German);
        (await author.SendAsync(HttpMethod.Post, "/api/v1/manage/lessons", new { unitId = germanUnit, title = "German draft", objective = "Basic words", explanation = "English instructions", orderNumber = 1 }, combined)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RejectedVersionsCanBeEditedButSubmittedAndPublishedVersionsCannot()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var lesson = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        await SubmitAsync(author, writer.Token, lesson.Id, lesson.Version);
        var update = new { versionId = lesson.Version, title = "Revised title", objective = "Learn a greeting", explanation = "New English explanation" };
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/lessons/{lesson.Id}", update, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.Version, "ChangesRequested")).StatusCode.Should().Be(HttpStatusCode.OK);
        var returned = await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>();
        returned!.Reviews.Should().ContainSingle(item => item.Decision == "ChangesRequested" && item.Comment.Contains("Reviewed"));
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/lessons/{lesson.Id}", update, writer.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.Version)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PrerequisitesRejectCyclesCrossCourseAndConcurrentCycles()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid unit = await UnitAsync(admin, administrator.Token); var a = await DraftAsync(author, writer.Token, unit); var b = await DraftAsync(author, writer.Token, unit, 2);
        Task<HttpResponseMessage> Edges(Guid lesson, Guid parent) => author.SendAsync(HttpMethod.Put, $"/api/v1/manage/lessons/{lesson}/prerequisites", new { lessonIds = new[] { parent } }, writer.Token);
        var responses = await Task.WhenAll(Edges(a.Id, b.Id), Edges(b.Id, a.Id));
        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        (await Edges(a.Id, a.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var other = Browser(); var german = await StaffAsync(other, RoleNames.ContentAuthor, 4);
        var foreign = await DraftAsync(other, german.Token, await UnitAsync(admin, administrator.Token, German));
        (await Edges(a.Id, foreign.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UnknownFormatsMissingSpeakingAudioAndMissingAnswersCannotBeSubmitted()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        Guid unit = await UnitAsync(admin, administrator.Token);
        var unsupported = await DraftAsync(author, writer.Token, unit, type: "Speaking");
        (await SubmitAsync(author, writer.Token, unsupported.Id, unsupported.Version)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var valid = await DraftAsync(author, writer.Token, unit, 2);
        var unknown = await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{valid.Id}/exercises", new { versionId = valid.Version, type = "UnknownFormat", prompt = "Test", explanation = "Test", orderNumber = 2, options = Array.Empty<string>(), answers = new[] { "Hola" } }, writer.Token);
        unknown.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using (var scope = fixture.Services.CreateAsyncScope()) { var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>(); await database.AcceptedAnswers.Where(answer => database.Exercises.Any(exercise => exercise.Id == answer.ExerciseId && exercise.LessonVersionId == valid.Version)).ExecuteDeleteAsync(); }
        (await SubmitAsync(author, writer.Token, valid.Id, valid.Version)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await fixture.Client.GetAsync($"/api/v1/manage/lessons/{valid.Id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
