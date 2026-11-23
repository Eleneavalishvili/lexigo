using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Content;

public sealed partial class PublishingTests
{
    [Theory]
    [InlineData("Reading", "Me llamo Ana. Vivo en Madrid.", "Where does Ana live?", "Madrid", "Barcelona")]
    [InlineData("DialogueChoice", "En el café. Camarero: Buenos días. ¿Qué desea?", "Choose a polite reply.", "Un café, por favor.", "Me gusta el tren.")]
    public async Task ContextIsReviewedSnapshottedAndPreservedWhenTheNextVersionChanges(string type, string context, string prompt, string correct, string incorrect)
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        using var reviewer = Browser(); var reader = await StaffAsync(reviewer, RoleNames.Reviewer);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), type, prompt, [correct, incorrect], [correct], contextText: context);
        lesson.Exercises[0].ContextText.Should().Be(context);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, lesson.VersionId)).EnsureSuccessStatusCode();
        (await author.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId = Spanish }, writer.Token)).EnsureSuccessStatusCode();
        var start = await author.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.Id}/sessions", bearer: writer.Token);
        start.EnsureSuccessStatusCode(); var session = (await start.Content.ReadFromJsonAsync<SessionView>())!;
        session.CurrentExercise!.ContextText.Should().Be(context);
        (await start.Content.ReadAsStringAsync()).Should().NotContain("correctOptionIds").And.NotContain("evaluationRules").And.NotContain("privateEvaluation");
        Guid revision = await RevisionAsync(author, writer.Token, lesson.Id);
        var copied = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/manage/lessons/{lesson.Id}", bearer: writer.Token)).Content.ReadFromJsonAsync<StaffLesson>())!;
        copied.Exercises[0].ContextText.Should().Be(context);
        const string changed = "A different passage or conversation for the next version.";
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/manage/lessons/{lesson.Id}/exercises/{copied.Exercises[0].Id}", new
        {
            versionId = revision,
            type,
            prompt,
            contextText = changed,
            explanation = "English feedback",
            orderNumber = 1,
            options = new[] { correct, incorrect },
            answers = new[] { correct }
        }, writer.Token)).EnsureSuccessStatusCode();
        (await SubmitAsync(author, writer.Token, lesson.Id, revision)).EnsureSuccessStatusCode();
        (await ReviewAsync(reviewer, reader.Token, lesson.Id, revision)).EnsureSuccessStatusCode();
        (await PublishAsync(admin, administrator.Token, lesson.Id, revision)).EnsureSuccessStatusCode();
        var saved = (await (await author.SendAsync(HttpMethod.Get, $"/api/v1/me/learning-sessions/{session.Id}", bearer: writer.Token)).Content.ReadFromJsonAsync<SessionView>())!;
        saved.CurrentExercise!.ContextText.Should().Be(context);
        Guid current = session.CurrentExercise.Id;
        foreach (var payload in new object[] {
            new { sessionExerciseId = current, selectedOptionIds = new[] { Guid.NewGuid() }, idempotencyKey = Guid.NewGuid() },
            new { sessionExerciseId = current, answerText = correct, selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }
        }) (await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", payload, writer.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        Guid correctId = saved.CurrentExercise.Options.Single(item => item.Text == correct).Id;
        var answer = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers", new { sessionExerciseId = current, selectedOptionIds = new[] { correctId }, idempotencyKey = Guid.NewGuid() }, writer.Token);
        answer.EnsureSuccessStatusCode(); (await answer.Content.ReadFromJsonAsync<AnswerFeedback>())!.IsCorrect.Should().BeTrue();
        var complete = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: writer.Token);
        complete.EnsureSuccessStatusCode(); var result = (await complete.Content.ReadFromJsonAsync<SessionView>())!;
        result.Passed.Should().BeTrue(); result.AwardedXp.Should().Be(20); result.Results[0].ContextText.Should().Be(context);
        var next = (await (await author.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lesson.Id}/sessions", bearer: writer.Token)).Content.ReadFromJsonAsync<SessionView>())!;
        next.CurrentExercise!.ContextText.Should().Be(changed);
        var wrong = await author.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{next.Id}/answers", new { sessionExerciseId = next.CurrentExercise.Id, selectedOptionIds = new[] { next.CurrentExercise.Options.Single(item => item.Text == incorrect).Id }, idempotencyKey = Guid.NewGuid() }, writer.Token);
        wrong.EnsureSuccessStatusCode(); var feedback = (await wrong.Content.ReadFromJsonAsync<AnswerFeedback>())!;
        feedback.IsCorrect.Should().BeFalse(); feedback.HeartsRemaining.Should().Be(4);
    }

    [Theory]
    [InlineData("Reading", null)]
    [InlineData("Reading", "   ")]
    [InlineData("DialogueChoice", null)]
    [InlineData("DialogueChoice", "   ")]
    public async Task MissingContextPreventsSubmission(string type, string? context)
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), type, "Choose the answer.", ["One", "Two"], ["One"], contextText: context);
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("Reading")]
    [InlineData("DialogueChoice")]
    public async Task ContextDoesNotBypassChoiceValidation(string type)
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var lesson = await TextDraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token), type, "Choose.", ["One"], ["One"], contextText: "A short passage or conversation.");
        (await SubmitAsync(author, writer.Token, lesson.Id, lesson.VersionId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task IrrelevantOversizedAndNullCharacterContextsAreRejectedWithoutSaving()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var lesson = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        foreach (var (type, context, expected) in new[] {
            ("MultipleChoice", "Hidden context", HttpStatusCode.Conflict),
            ("Reading", new string('x', 4001), HttpStatusCode.BadRequest),
            ("DialogueChoice", "Contains\0null", HttpStatusCode.Conflict)
        })
        {
            (await author.SendAsync(HttpMethod.Post, $"/api/v1/manage/lessons/{lesson.Id}/exercises", new { versionId = lesson.Version, type, contextText = context, prompt = "Choose", explanation = "English feedback", orderNumber = 2, options = new[] { "One", "Two" }, answers = new[] { "One" } }, writer.Token)).StatusCode.Should().Be(expected);
        }
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.Exercises.CountAsync(item => item.LessonVersionId == lesson.Version)).Should().Be(1);
    }

    [Fact]
    public async Task PostgreSqlEnforcesThePassageBoundAndOldExercisesRemainNullable()
    {
        using var admin = Browser(); var administrator = await StaffAsync(admin, RoleNames.Admin);
        using var author = Browser(); var writer = await StaffAsync(author, RoleNames.ContentAuthor);
        var lesson = await DraftAsync(author, writer.Token, await UnitAsync(admin, administrator.Token));
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var exercise = await db.Exercises.SingleAsync(item => item.LessonVersionId == lesson.Version);
        exercise.ContextText.Should().BeNull();
        string tooLong = new('x', 4001);
        await FluentActions.Invoking(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Exercises\" SET \"ContextText\"={tooLong} WHERE \"Id\"={exercise.Id}"))
            .Should().ThrowAsync<PostgresException>().Where(error => error.SqlState == PostgresErrorCodes.StringDataRightTruncation);
        const string oldJson = """{"id":"00000000-0000-0000-0000-000000000001","type":"Translation","prompt":"Hello","options":[]}""";
        JsonSerializer.Deserialize<ExerciseView>(oldJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.ContextText.Should().BeNull();
    }
}
