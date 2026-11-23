using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Classrooms;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Tests.Accounts;
using LanguageLearning.Tests.Learning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Classrooms;

public sealed partial class ClassroomTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private async Task<(SecurityTestAccount User, string Token)> Account(SessionTestBrowser browser, string role = "Learner")
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var account = (await users.FindByIdAsync(user.Id.ToString()))!;
        if (!await users.IsInRoleAsync(account, role)) (await users.AddToRoleAsync(account, role)).Succeeded.Should().BeTrue();
        return (user, await user.LoginAsync(browser));
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private async Task<(Guid Id, Guid CourseId)> Create(SessionTestBrowser browser, string token, Guid teacherId)
    {
        var content = await LearningTestContent.CreateAsync(fixture, teacherId);
        var result = await Read<ClassroomWriteResponse>(await browser.SendAsync(HttpMethod.Post, "/api/v1/classes",
            new CreateClassroomInput(content.CourseId, "Spanish together", "Weekly language group", Guid.NewGuid()), token));
        return (result.Id, content.CourseId);
    }
    private static async Task<InvitationCreated> Invite(SessionTestBrowser browser, string token, Guid id, int maxUses = 10) =>
        await Read<InvitationCreated>(await browser.SendAsync(HttpMethod.Post, $"/api/v1/classes/{id}/invitations", new { days = 1, maxUses }, token));
    private static Task<HttpResponseMessage> Join(SessionTestBrowser browser, string token, string code) =>
        browser.SendAsync(HttpMethod.Post, "/api/v1/classes/join", new { code }, token);

    [Fact]
    public async Task ExtraStudyListsOnlyOptedInTeachersForAnActivePremiumLearner()
    {
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(teacherBrowser, "Teacher");
        var learner = await Account(learnerBrowser);
        var content = await LearningTestContent.CreateAsync(fixture, teacher.User.Id);
        var availability = $"/api/v1/extra-study/availability/{content.CourseId}";
        var directory = $"/api/v1/extra-study/courses/{content.CourseId}/teachers";
        (await learnerBrowser.SendAsync(HttpMethod.Put, availability, new { enabled = true }, learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await teacherBrowser.SendAsync(HttpMethod.Put, availability, new { enabled = true }, teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await learnerBrowser.SendAsync(HttpMethod.Get, directory, bearer: learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var premium = LanguageLearning.Infrastructure.Billing.BillingCatalogSeed.Premium;
        var priceId = await database.PlanPrices.Where(price => price.PlanId == premium).Select(price => price.Id).FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var subscription = new LanguageLearning.Core.Billing.Subscription
        {
            UserId = learner.User.Id, PlanId = premium, CurrentPlanPriceId = priceId, Status = "Active",
            CreatedAt = now, CurrentPeriodStart = now.AddMinutes(-2), CurrentPeriodEnd = now.AddDays(1),
        };
        database.Subscriptions.Add(subscription);
        await database.SaveChangesAsync();

        var entries = await Read<System.Text.Json.JsonElement>(
            await learnerBrowser.SendAsync(HttpMethod.Get, directory, bearer: learner.Token));
        var entry = Assert.Single(entries.EnumerateArray());
        entry.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "displayName");
        entry.GetProperty("id").GetGuid().Should().NotBe(teacher.User.Id);
        var offerId = entry.GetProperty("id").GetGuid();
        var requestPath = $"/api/v1/extra-study/offers/{offerId}/requests";
        var sends = await Task.WhenAll(
            learnerBrowser.SendAsync(HttpMethod.Post, requestPath, bearer: learner.Token),
            learnerBrowser.SendAsync(HttpMethod.Post, requestPath, bearer: learner.Token));
        var request = await Read<System.Text.Json.JsonElement>(sends[0]);
        var requestId = request.GetProperty("id").GetGuid();
        (await Read<System.Text.Json.JsonElement>(sends[1])).GetProperty("id").GetGuid().Should().Be(requestId);
        var decision = $"/api/v1/extra-study/requests/{requestId}/decision";
        var space = $"/api/v1/extra-study/requests/{requestId}/space";
        (await learnerBrowser.SendAsync(HttpMethod.Get, space, bearer: learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var otherTeacherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var otherTeacher = await Account(otherTeacherBrowser, "Teacher");
        (await otherTeacherBrowser.SendAsync(HttpMethod.Put, decision, new { accepted = true }, otherTeacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Read<ExtraStudyRequestView[]>(await otherTeacherBrowser.SendAsync(
            HttpMethod.Get, "/api/v1/extra-study/requests", bearer: otherTeacher.Token))).Should().BeEmpty();
        (await learnerBrowser.SendAsync(HttpMethod.Put, decision, new { accepted = true }, learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await teacherBrowser.SendAsync(HttpMethod.Put, decision, new { accepted = true }, teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await teacherBrowser.SendAsync(HttpMethod.Put, decision, new { accepted = false }, teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var requestList = await Read<ExtraStudyRequestView[]>(await learnerBrowser.SendAsync(
            HttpMethod.Get, "/api/v1/extra-study/requests", bearer: learner.Token));
        Assert.Single(requestList).Status.Should().Be("Accepted");
        (await otherTeacherBrowser.SendAsync(HttpMethod.Get, space, bearer: otherTeacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var foreignContent = await LearningTestContent.CreateAsync(fixture, otherTeacher.User.Id, courseNumber: 2);
        (await teacherBrowser.SendAsync(HttpMethod.Put, $"{space}/lessons/{foreignContent.LessonId}", bearer: teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var share = $"{space}/lessons/{content.LessonId}";
        (await learnerBrowser.SendAsync(HttpMethod.Put, share, bearer: learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var sharing = await Task.WhenAll(
            teacherBrowser.SendAsync(HttpMethod.Put, share, bearer: teacher.Token),
            teacherBrowser.SendAsync(HttpMethod.Put, share, bearer: teacher.Token));
        sharing.Should().OnlyContain(response => response.IsSuccessStatusCode);
        var learnerSpace = await Read<ExtraStudySpaceView>(await learnerBrowser.SendAsync(
            HttpMethod.Get, space, bearer: learner.Token));
        learnerSpace.CanShare.Should().BeFalse();
        Assert.Single(learnerSpace.Lessons).Id.Should().Be(content.LessonId);
        (await database.ExtraStudyRequests.CountAsync(item => item.OfferId == offerId)).Should().Be(1);

        (await teacherBrowser.SendAsync(HttpMethod.Put, availability, new { enabled = false }, teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Read<ExtraStudyTeacher[]>(await learnerBrowser.SendAsync(HttpMethod.Get, directory, bearer: learner.Token)))
            .Should().BeEmpty();

        subscription.CurrentPeriodEnd = now.AddMinutes(-1);
        await database.SaveChangesAsync();
        (await learnerBrowser.SendAsync(HttpMethod.Get, space, bearer: learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await learnerBrowser.SendAsync(HttpMethod.Get, directory, bearer: learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var close = $"/api/v1/extra-study/requests/{requestId}";
        (await otherTeacherBrowser.SendAsync(HttpMethod.Delete, close, bearer: otherTeacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await learnerBrowser.SendAsync(HttpMethod.Delete, close, bearer: learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await teacherBrowser.SendAsync(HttpMethod.Delete, close, bearer: teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await teacherBrowser.SendAsync(HttpMethod.Get, space, bearer: teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await teacherBrowser.SendAsync(HttpMethod.Put, decision, new { accepted = true }, teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var closedRequests = await Read<ExtraStudyRequestView[]>(await learnerBrowser.SendAsync(
            HttpMethod.Get, "/api/v1/extra-study/requests", bearer: learner.Token));
        Assert.Single(closedRequests).Status.Should().Be("Closed");
    }

    [Theory]
    [InlineData("Learner")]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    [InlineData("ContentAuthor")]
    [InlineData("Reviewer")]
    [InlineData("Moderator")]
    public async Task TeacherRoleIsRequiredRatherThanAnImplicitAdminBypass(string role)
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var account = await Account(browser, role);
        var content = await LearningTestContent.CreateAsync(fixture, account.User.Id);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/classes",
            new CreateClassroomInput(content.CourseId, "A class", "", Guid.NewGuid()), account.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignmentsAcceptPublishedCourseLessonsAndArePrivateToTheClass()
    {
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var outsiderBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(teacherBrowser, "Teacher");
        var learner = await Account(learnerBrowser);
        var outsider = await Account(outsiderBrowser, "Teacher");
        var classroom = await Create(teacherBrowser, teacher.Token, teacher.User.Id);
        var lesson = await LearningTestContent.CreateAsync(fixture, teacher.User.Id);
        var foreign = await LearningTestContent.CreateAsync(fixture, teacher.User.Id, courseNumber: 2);
        var path = $"/api/v1/classes/{classroom.Id}/assignments";
        var input = new CreateAssignmentInput("Greetings practice", DateTimeOffset.UtcNow.AddDays(2), 80,
            [lesson.LessonId], Guid.NewGuid());

        (await learnerBrowser.SendAsync(HttpMethod.Post, path, input, learner.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outsiderBrowser.SendAsync(HttpMethod.Post, path, input, outsider.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await teacherBrowser.SendAsync(HttpMethod.Post, path, input with { LessonIds = [foreign.LessonId] }, teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var responses = await Task.WhenAll(
            teacherBrowser.SendAsync(HttpMethod.Post, path, input, teacher.Token),
            teacherBrowser.SendAsync(HttpMethod.Post, path, input, teacher.Token));
        using var created = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        using var replay = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var assignment = await Read<ClassroomWriteResponse>(created);
        (await Read<ClassroomWriteResponse>(replay)).Id.Should().Be(assignment.Id);
        Assert.NotNull(created.Headers.Location);
        var detailPath = created.Headers.Location.AbsolutePath;
        Assert.Equal($"/api/v1/assignments/{assignment.Id}", detailPath);
        using var teacherDetail = await teacherBrowser.SendAsync(HttpMethod.Get, detailPath, bearer: teacher.Token);
        Assert.Equal(HttpStatusCode.OK, teacherDetail.StatusCode);
        (await teacherBrowser.SendAsync(HttpMethod.Post, path, input with { MinimumScore = 85 }, teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await outsiderBrowser.SendAsync(HttpMethod.Get, detailPath, bearer: outsider.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsiderBrowser.SendAsync(HttpMethod.Get, path, bearer: outsider.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await learnerBrowser.SendAsync(HttpMethod.Get, detailPath, bearer: learner.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var invitation = await Invite(teacherBrowser, teacher.Token, classroom.Id);
        (await Join(learnerBrowser, learner.Token, invitation.Code)).EnsureSuccessStatusCode();
        var list = await Read<ClassPage<AssignmentSummary>>(await learnerBrowser.SendAsync(HttpMethod.Get, path, bearer: learner.Token));
        list.Items.Should().ContainSingle(item => item.Id == assignment.Id && item.MinimumScore == 80);
        var detail = await Read<AssignmentDetail>(await learnerBrowser.SendAsync(HttpMethod.Get, detailPath, bearer: learner.Token));
        detail.Lessons.Should().ContainSingle(item => item.Id == lesson.LessonId && item.IsPublished);

        (await learnerBrowser.SendAsync(HttpMethod.Delete, $"/api/v1/classes/{classroom.Id}/members/{learner.User.Id}",
            bearer: learner.Token)).EnsureSuccessStatusCode();
        (await learnerBrowser.SendAsync(HttpMethod.Get, detailPath, bearer: learner.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignmentEditsAreOwnerOnlyAndConcurrentStaleWritesAreRejected()
    {
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var otherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(teacherBrowser, "Teacher");
        var other = await Account(otherBrowser, "Teacher");
        var classroom = await Create(teacherBrowser, teacher.Token, teacher.User.Id);
        var first = await LearningTestContent.CreateAsync(fixture, teacher.User.Id);
        var second = await LearningTestContent.CreateAsync(fixture, teacher.User.Id);
        var creation = new CreateAssignmentInput("Original", DateTimeOffset.UtcNow.AddDays(1), 80,
            [first.LessonId], Guid.NewGuid());
        var createPath = $"/api/v1/classes/{classroom.Id}/assignments";
        var assignment = await Read<ClassroomWriteResponse>(
            await teacherBrowser.SendAsync(HttpMethod.Post, createPath, creation, teacher.Token));
        var path = $"/api/v1/assignments/{assignment.Id}";
        var edit = new EditAssignmentInput("Updated", creation.DueAt.AddDays(1), 90, [second.LessonId], 1);
        (await otherBrowser.SendAsync(HttpMethod.Get, path + "/results", bearer: other.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherBrowser.SendAsync(HttpMethod.Patch, path, edit, other.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await teacherBrowser.SendAsync(HttpMethod.Patch, path, new { edit.Title, edit.DueAt, edit.LessonIds, edit.Revision },
            teacher.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await teacherBrowser.SendAsync(HttpMethod.Patch, path, edit with { DueAt = DateTimeOffset.UtcNow.AddDays(-1) },
            teacher.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var responses = await Task.WhenAll(
            teacherBrowser.SendAsync(HttpMethod.Patch, path, edit, teacher.Token),
            teacherBrowser.SendAsync(HttpMethod.Patch, path, edit, teacher.Token));
        responses.Select(response => response.StatusCode)
            .Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });

        var updated = await Read<AssignmentDetail>(await teacherBrowser.SendAsync(HttpMethod.Get, path, bearer: teacher.Token));
        updated.Revision.Should().Be(2);
        updated.Title.Should().Be("Updated");
        updated.MinimumScore.Should().Be(90);
        updated.DueAt.Should().BeCloseTo(edit.DueAt, TimeSpan.FromMilliseconds(1));
        updated.Lessons.Should().ContainSingle(item => item.Id == second.LessonId);
        var replay = await Read<ClassroomWriteResponse>(
            await teacherBrowser.SendAsync(HttpMethod.Post, createPath, creation, teacher.Token));
        replay.Id.Should().Be(assignment.Id);

        (await teacherBrowser.SendAsync(HttpMethod.Patch, $"/api/v1/classes/{classroom.Id}",
            new EditClassroomInput("Archived", "", true, 1), teacher.Token)).EnsureSuccessStatusCode();
        (await teacherBrowser.SendAsync(HttpMethod.Patch, path, edit with { Revision = 2 },
            teacher.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LearnersSubmitOwnedGradedAttemptsOnceAndServerRecordsLateness()
    {
        var clock = new Clock { Now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) };
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateBillingClient(clock));
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateBillingClient(clock));
        using var otherBrowser = new SessionTestBrowser(fixture.CreateBillingClient(clock));
        var teacher = await Account(teacherBrowser, "Teacher");
        var learner = await Account(learnerBrowser);
        var other = await Account(otherBrowser);
        var classroom = await Create(teacherBrowser, teacher.Token, teacher.User.Id);
        var lesson = await LearningTestContent.CreateAsync(fixture, teacher.User.Id, count: 1);
        var invitation = await Invite(teacherBrowser, teacher.Token, classroom.Id);
        (await Join(learnerBrowser, learner.Token, invitation.Code)).EnsureSuccessStatusCode();
        (await Join(otherBrowser, other.Token, invitation.Code)).EnsureSuccessStatusCode();
        var old = await FinishAssignedLesson(learnerBrowser, learner.Token, lesson.LessonId, practice: false);
        clock.Now = clock.Now.AddSeconds(1);
        var dueAt = clock.Now.AddMinutes(1);
        var created = await Read<ClassroomWriteResponse>(await teacherBrowser.SendAsync(HttpMethod.Post,
            $"/api/v1/classes/{classroom.Id}/assignments",
            new CreateAssignmentInput("Greetings practice", dueAt, 80, [lesson.LessonId], Guid.NewGuid()), teacher.Token));
        var path = $"/api/v1/assignments/{created.Id}";
        Task<HttpResponseMessage> Submit(SessionTestBrowser browser, string token, Guid sessionId) =>
            browser.SendAsync(HttpMethod.Post, path + "/submissions", new SubmitAssignmentInput(sessionId), token);

        (await Submit(learnerBrowser, learner.Token, old.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var lessonSession = await FinishAssignedLesson(learnerBrowser, learner.Token, lesson.LessonId, practice: false);
        (await Submit(otherBrowser, other.Token, lessonSession.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var otherDetail = await Read<AssignmentDetail>(await otherBrowser.SendAsync(HttpMethod.Get, path, bearer: other.Token));
        otherDetail.LearnerWork!.Available.Should().BeEmpty();
        var learnerDetail = await Read<AssignmentDetail>(await learnerBrowser.SendAsync(HttpMethod.Get, path, bearer: learner.Token));
        learnerDetail.LearnerWork!.Available.Should().ContainSingle(item => item.SessionId == lessonSession.Id && item.Score == 100);

        clock.Now = dueAt;
        var responses = await Task.WhenAll(
            Submit(learnerBrowser, learner.Token, lessonSession.Id),
            Submit(learnerBrowser, learner.Token, lessonSession.Id));
        var receipt = await Read<AssignmentReceipt>(responses[0]);
        receipt.IsLate.Should().BeFalse();
        (await Read<AssignmentReceipt>(responses[1])).Id.Should().Be(receipt.Id);

        var edit = new EditAssignmentInput("Updated title", dueAt, 80, [lesson.LessonId], 1);
        (await teacherBrowser.SendAsync(HttpMethod.Patch, path, edit with { DueAt = dueAt.AddHours(1) },
            teacher.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await teacherBrowser.SendAsync(HttpMethod.Patch, path, edit, teacher.Token)).EnsureSuccessStatusCode();
        clock.Now = dueAt.AddSeconds(1).AddTicks(7);
        var practice = await FinishAssignedLesson(learnerBrowser, learner.Token, lesson.LessonId, practice: true);
        var late = await Read<AssignmentReceipt>(await Submit(learnerBrowser, learner.Token, practice.Id));
        late.IsLate.Should().BeTrue();
        (await Read<AssignmentReceipt>(await Submit(learnerBrowser, learner.Token, practice.Id))).Should().Be(late);
        (await Read<AssignmentReceipt>(await Submit(learnerBrowser, learner.Token, lessonSession.Id))).Should().Be(receipt);

        learnerDetail = await Read<AssignmentDetail>(await learnerBrowser.SendAsync(HttpMethod.Get, path, bearer: learner.Token));
        learnerDetail.LearnerWork!.Available.Should().BeEmpty();
        learnerDetail.LearnerWork.Submitted.Should().ContainSingle(item => item.LessonId == lesson.LessonId && item.IsLate);
        var teacherDetail = await Read<AssignmentDetail>(await teacherBrowser.SendAsync(HttpMethod.Get, path, bearer: teacher.Token));
        teacherDetail.LearnerWork.Should().BeNull();
        (await learnerBrowser.SendAsync(HttpMethod.Get, path + "/results", bearer: learner.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var resultResponse = await teacherBrowser.SendAsync(HttpMethod.Get, path + "/results", bearer: teacher.Token);
        resultResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        var results = await Read<AssignmentResultsPage>(resultResponse);
        results.RequiredLessons.Should().Be(1);
        results.Learners.TotalCount.Should().Be(2);
        var learnerResult = results.Learners.Items.Single(item => item.LearnerId == learner.User.Id);
        learnerResult.Status.Should().Be("Completed");
        learnerResult.Lessons.Should().ContainSingle(item => item.AttemptCount == 2 && item.BestScore == 100
            && item.LatestAccuracy == 100 && item.LatestSubmissionId == late.Id && item.IsLate);
        results.Learners.Items.Should().ContainSingle(item => item.LearnerId == other.User.Id
            && item.Status == "NotSubmitted" && item.Lessons.Length == 0);
        var resultJson = await resultResponse.Content.ReadAsStringAsync();
        resultJson.Should().NotContain(old.Id.ToString()).And.NotContain(practice.Id.ToString()).And.NotContain("answerPayload");
        var feedbackPath = path + "/feedback";
        var input = new WriteAssignmentFeedback(late.Id, "Try saying the phrase aloud.");
        (await learnerBrowser.SendAsync(HttpMethod.Post, feedbackPath, input, learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await teacherBrowser.SendAsync(HttpMethod.Post, feedbackPath, input with { SubmissionId = Guid.NewGuid() }, teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var feedbackWrites = await Task.WhenAll(
            teacherBrowser.SendAsync(HttpMethod.Post, feedbackPath, input, teacher.Token),
            teacherBrowser.SendAsync(HttpMethod.Post, feedbackPath, input, teacher.Token));
        (await Read<ClassroomWriteResponse>(feedbackWrites[0])).Id
            .Should().Be((await Read<ClassroomWriteResponse>(feedbackWrites[1])).Id);
        (await teacherBrowser.SendAsync(HttpMethod.Post, feedbackPath, input with { Message = "Different feedback" }, teacher.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        learnerDetail = await Read<AssignmentDetail>(await learnerBrowser.SendAsync(HttpMethod.Get, path, bearer: learner.Token));
        learnerDetail.LearnerWork!.Feedback.Should().ContainSingle(note => note.Message == input.Message);
        otherDetail = await Read<AssignmentDetail>(await otherBrowser.SendAsync(HttpMethod.Get, path, bearer: other.Token));
        otherDetail.LearnerWork!.Feedback.Should().BeEmpty();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            (await database.Notifications.CountAsync(note => note.UserId == learner.User.Id && note.Type == "AssignmentFeedback"))
                .Should().Be(1);
        }
        (await learnerBrowser.SendAsync(HttpMethod.Delete, $"/api/v1/classes/{classroom.Id}/members/{learner.User.Id}",
            bearer: learner.Token)).EnsureSuccessStatusCode();
        (await Submit(learnerBrowser, learner.Token, practice.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        results = await Read<AssignmentResultsPage>(await teacherBrowser.SendAsync(HttpMethod.Get, path + "/results", bearer: teacher.Token));
        results.Learners.Items.Should().ContainSingle(item => item.LearnerId == learner.User.Id && item.MembershipStatus == "Left");
    }

    private static async Task<SessionView> FinishAssignedLesson(SessionTestBrowser browser, string token, Guid lessonId, bool practice)
    {
        var session = await Read<SessionView>(practice
            ? await browser.SendAsync(HttpMethod.Post, "/api/v1/me/practice-sessions", new { lessonId }, token)
            : await browser.SendAsync(HttpMethod.Post, $"/api/v1/lessons/{lessonId}/sessions", bearer: token));
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new { sessionExerciseId = session.CurrentExercise!.Id, answerText = "Hola", selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, token))
            .EnsureSuccessStatusCode();
        return await Read<SessionView>(await browser.SendAsync(HttpMethod.Post,
            $"/api/v1/me/learning-sessions/{session.Id}/complete", bearer: token));
    }

    [Fact]
    public async Task AssignmentNoticesArePrivateAndDeadlineRetriesDoNotDuplicateThem()
    {
        var clock = new Clock { Now = DateTimeOffset.UtcNow };
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateBillingClient(clock));
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateBillingClient(clock));
        using var completedBrowser = new SessionTestBrowser(fixture.CreateBillingClient(clock));
        var teacher = await Account(teacherBrowser, "Teacher");
        var learner = await Account(learnerBrowser);
        var completed = await Account(completedBrowser);
        var classroom = await Create(teacherBrowser, teacher.Token, teacher.User.Id);
        var lesson = await LearningTestContent.CreateAsync(fixture, teacher.User.Id, count: 1);
        var invitation = await Invite(teacherBrowser, teacher.Token, classroom.Id);
        (await Join(learnerBrowser, learner.Token, invitation.Code)).EnsureSuccessStatusCode();
        (await Join(completedBrowser, completed.Token, invitation.Code)).EnsureSuccessStatusCode();

        var path = $"/api/v1/classes/{classroom.Id}/assignments";
        var input = new CreateAssignmentInput("Upcoming practice", clock.Now.AddHours(25), 80,
            [lesson.LessonId], Guid.NewGuid());
        var created = await Read<ClassroomWriteResponse>(
            await teacherBrowser.SendAsync(HttpMethod.Post, path, input, teacher.Token));
        (await teacherBrowser.SendAsync(HttpMethod.Post, path, input, teacher.Token)).EnsureSuccessStatusCode();
        var session = await FinishAssignedLesson(completedBrowser, completed.Token, lesson.LessonId, practice: false);
        (await completedBrowser.SendAsync(HttpMethod.Post, $"/api/v1/assignments/{created.Id}/submissions",
            new SubmitAssignmentInput(session.Id), completed.Token)).EnsureSuccessStatusCode();

        async Task RunBatchAsync()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await new AssignmentDeadlineNotices(database, clock).SendBatchAsync(CancellationToken.None);
        }

        await RunBatchAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var notices = database.Notifications.AsNoTracking().Where(note => note.AssignmentId == created.Id);
        var initial = await notices.ToArrayAsync();
        initial.Should().OnlyContain(note => note.Type == "AssignmentCreated");
        initial.Select(note => note.UserId).Should().BeEquivalentTo(new[] { learner.User.Id, completed.User.Id });

        clock.Now = clock.Now.AddHours(2);
        await Task.WhenAll(RunBatchAsync(), RunBatchAsync());
        var saved = await notices.ToArrayAsync();
        saved.Should().HaveCount(3);
        saved.Should().ContainSingle(note => note.Type == "AssignmentDue" && note.UserId == learner.User.Id);
    }

    [Fact]
    public async Task ContentReportsProtectExerciseOwnershipAndNotifyTheReporterOnce()
    {
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var moderatorBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var learner = await Account(learnerBrowser);
        var moderator = await Account(moderatorBrowser, "Moderator");
        var lesson = await LearningTestContent.CreateAsync(fixture, learner.User.Id, count: 1);
        (await learnerBrowser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments",
            new { lesson.CourseId }, learner.Token)).EnsureSuccessStatusCode();
        var session = await Read<SessionView>(await learnerBrowser.SendAsync(HttpMethod.Post,
            $"/api/v1/lessons/{lesson.LessonId}/sessions", bearer: learner.Token));
        var input = new CreateContentReport(session.CurrentExercise!.Id, "Text", "Please review the wording.");
        (await moderatorBrowser.SendAsync(HttpMethod.Post, "/api/v1/reports", input, moderator.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var requests = await Task.WhenAll(
            learnerBrowser.SendAsync(HttpMethod.Post, "/api/v1/reports", input, learner.Token),
            learnerBrowser.SendAsync(HttpMethod.Post, "/api/v1/reports", input, learner.Token));
        using var created = Assert.Single(requests, response => response.StatusCode == HttpStatusCode.Created);
        using var replay = Assert.Single(requests, response => response.StatusCode == HttpStatusCode.OK);
        var report = await Read<ContentReportView>(created);
        Assert.Equal(report, await Read<ContentReportView>(replay));
        Assert.NotNull(created.Headers.Location);
        var privatePath = created.Headers.Location.AbsolutePath;
        Assert.Equal($"/api/v1/me/reports/{report.Id}", privatePath);
        using var ownerRead = await learnerBrowser.SendAsync(HttpMethod.Get, privatePath, bearer: learner.Token);
        Assert.Equal(report, await Read<ContentReportView>(ownerRead));
        using var foreignRead = await moderatorBrowser.SendAsync(HttpMethod.Get, privatePath, bearer: moderator.Token);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        (await learnerBrowser.SendAsync(HttpMethod.Post, "/api/v1/reports", input with { Comment = "Different wording." }, learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        report.Status.Should().Be("New");
        report.VersionId.Should().Be(session.VersionId);
        var path = $"/api/v1/admin/reports/{report.Id}";
        var decision = new ReviewContentReport("Resolved", "We corrected the wording.");
        (await learnerBrowser.SendAsync(HttpMethod.Patch, path, decision, learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await moderatorBrowser.SendAsync(HttpMethod.Patch, path, new ReviewContentReport("InReview", null),
            moderator.Token)).EnsureSuccessStatusCode();
        var writes = await Task.WhenAll(
            moderatorBrowser.SendAsync(HttpMethod.Patch, path, decision, moderator.Token),
            moderatorBrowser.SendAsync(HttpMethod.Patch, path, decision, moderator.Token));
        foreach (var write in writes)
            (await Read<ContentReportView>(write)).Status.Should().Be("Resolved");
        var mine = await Read<ContentReportView[]>(await learnerBrowser.SendAsync(
            HttpMethod.Get, "/api/v1/me/reports", bearer: learner.Token));
        mine.Should().ContainSingle(item => item.Id == report.Id && item.Resolution == decision.Resolution);
        using var reviewed = await learnerBrowser.SendAsync(HttpMethod.Get, privatePath, bearer: learner.Token);
        Assert.Equal(decision.Resolution, (await Read<ContentReportView>(reviewed)).Resolution);
        Assert.DoesNotContain("resolvedBy", await reviewed.Content.ReadAsStringAsync());
        (await Read<ContentReportView[]>(await moderatorBrowser.SendAsync(
            HttpMethod.Get, "/api/v1/me/reports", bearer: moderator.Token))).Should().BeEmpty();
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.Notifications.CountAsync(item => item.UserId == learner.User.Id && item.Type == "ReportOutcome"))
            .Should().Be(1);
    }

    [Fact]
    public async Task AnalyticsFiltersGradedErrorsAndLimitsExportToAdministrators()
    {
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var adminBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var learner = await Account(learnerBrowser);
        var admin = await Account(adminBrowser, "Admin");
        var lesson = await LearningTestContent.CreateAsync(fixture, learner.User.Id, count: 1);
        (await learnerBrowser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments",
            new { lesson.CourseId }, learner.Token)).EnsureSuccessStatusCode();
        var session = await Read<SessionView>(await learnerBrowser.SendAsync(HttpMethod.Post,
            $"/api/v1/lessons/{lesson.LessonId}/sessions", bearer: learner.Token));
        const string privateAnswer = "private incorrect response";
        (await learnerBrowser.SendAsync(HttpMethod.Post, $"/api/v1/me/learning-sessions/{session.Id}/answers",
            new { sessionExerciseId = session.CurrentExercise!.Id, answerText = privateAnswer,
                selectedOptionIds = Array.Empty<Guid>(), idempotencyKey = Guid.NewGuid() }, learner.Token))
            .EnsureSuccessStatusCode();
        var today = DateTimeOffset.UtcNow;
        var path = $"/api/v1/admin/analytics?from={today.AddDays(-1):yyyy-MM-dd}&through={today:yyyy-MM-dd}";
        var scoped = path + $"&courseId={lesson.CourseId}";
        (await learnerBrowser.SendAsync(HttpMethod.Get, scoped + "&csv=true", bearer: learner.Token))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var report = await Read<LearningAnalyticsView>(await adminBrowser.SendAsync(HttpMethod.Get, scoped, bearer: admin.Token));
        report.CommonErrors.Should().ContainSingle(item => item.ExerciseId == lesson.ExerciseIds[0]
            && item.Answers == 1 && item.Errors == 1);
        report.Courses.Should().ContainSingle(item => item.CourseId == lesson.CourseId && item.Started > 0);
        var empty = await Read<LearningAnalyticsView>(await adminBrowser.SendAsync(
            HttpMethod.Get, path + $"&courseId={Guid.NewGuid()}", bearer: admin.Token));
        empty.ActiveLearners.Should().Be(0);
        empty.CommonErrors.Should().BeEmpty();
        var csv = await adminBrowser.SendAsync(HttpMethod.Get, scoped + "&csv=true", bearer: admin.Token);
        csv.EnsureSuccessStatusCode();
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var export = await csv.Content.ReadAsStringAsync();
        export.Should().Contain(lesson.ExerciseIds[0].ToString()).And.NotContain(privateAnswer)
            .And.NotContain(learner.User.Email);
    }

    [Fact]
    public async Task LessonCsvIsValidatedPrivatelyAndCommittedAsOneDraft()
    {
        using var authorBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var otherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var author = await Account(authorBrowser, "Admin");
        var other = await Account(otherBrowser, "Admin");
        var content = await LearningTestContent.CreateAsync(fixture, author.User.Id, count: 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var unitId = await database.Lessons.Where(lesson => lesson.Id == content.LessonId).Select(lesson => lesson.UnitId).SingleAsync();
        const string path = "/api/v1/manage/imports";
        var input = new ValidateLessonImport(unitId, 2, "Imported greetings", "Practice greeting people",
            "Review how to greet people politely.", "prompt,answer,feedback\n\"Translate hello, please\",Hola,\"A common greeting.\"");
        var invalid = await Read<LessonImportReview>(await authorBrowser.SendAsync(HttpMethod.Post, path + "/validate",
            input with { Csv = "prompt,answer,feedback\nMissing answer,,Feedback" }, author.Token));
        invalid.Status.Should().Be("Invalid");
        invalid.Errors.Should().ContainSingle(issue => issue.Row == 2);
        (await authorBrowser.SendAsync(HttpMethod.Post, $"{path}/{invalid.Id}/commit", bearer: author.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var validated = await authorBrowser.SendAsync(HttpMethod.Post, path + "/validate", input, author.Token);
        Assert.Equal(HttpStatusCode.Created, validated.StatusCode);
        var review = await Read<LessonImportReview>(validated);
        review.Status.Should().Be("Valid");
        review.ExerciseCount.Should().Be(1);
        Assert.Equal($"{path}/{review.Id}", validated.Headers.Location!.AbsolutePath);
        using var reportResponse = await authorBrowser.SendAsync(HttpMethod.Get, validated.Headers.Location.PathAndQuery, bearer: author.Token);
        Assert.True(reportResponse.Headers.CacheControl?.NoStore);
        var report = await Read<LessonImportJobStatus>(reportResponse);
        Assert.Equal(review.Id, report.Id);
        Assert.Equal("Valid", report.Status);
        Assert.Equal(review.ExerciseCount, report.ExerciseCount);
        Assert.Null(report.LessonId);
        Assert.Empty(report.Errors);
        Assert.Equal(HttpStatusCode.NotFound, (await otherBrowser.SendAsync(HttpMethod.Get,
            validated.Headers.Location.PathAndQuery, bearer: other.Token)).StatusCode);
        using (var reportJson = System.Text.Json.JsonDocument.Parse(await reportResponse.Content.ReadAsStringAsync()))
        {
            Assert.False(reportJson.RootElement.TryGetProperty("payload", out _));
            Assert.False(reportJson.RootElement.TryGetProperty("fileKey", out _));
        }
        (await database.Lessons.CountAsync(lesson => lesson.UnitId == unitId)).Should().Be(1);
        var commit = $"{path}/{review.Id}/commit";
        (await otherBrowser.SendAsync(HttpMethod.Post, commit, bearer: other.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var saves = await Task.WhenAll(
            authorBrowser.SendAsync(HttpMethod.Post, commit, bearer: author.Token),
            authorBrowser.SendAsync(HttpMethod.Post, commit, bearer: author.Token));
        using var created = Assert.Single(saves, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(saves, item => item.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(created.Headers.Location);
        using var detail = await authorBrowser.SendAsync(HttpMethod.Get,
            created.Headers.Location!.AbsolutePath, bearer: author.Token);
        detail.EnsureSuccessStatusCode();
        var first = await Read<System.Text.Json.JsonElement>(saves[0]);
        var second = await Read<System.Text.Json.JsonElement>(saves[1]);
        var lessonId = first.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/manage/lessons/{lessonId}", created.Headers.Location.AbsolutePath);
        second.GetProperty("id").GetGuid().Should().Be(lessonId);
        var committed = await Read<LessonImportJobStatus>(await authorBrowser.SendAsync(HttpMethod.Get,
            validated.Headers.Location.PathAndQuery, bearer: author.Token));
        Assert.Equal("Committed", committed.Status);
        Assert.Equal(lessonId, committed.LessonId);
        (await database.Lessons.Where(item => item.Id == lessonId).Select(item => item.Status).SingleAsync()).Should().Be("Draft");
        (await database.Lessons.CountAsync(item => item.UnitId == unitId)).Should().Be(2);
        (await database.AuditLogs.CountAsync(item => item.EntityId == review.Id.ToString() && item.Action == "LessonImportCommitted"))
            .Should().Be(1);
    }

    [Fact]
    public async Task CreationIsIdempotentAndEditsUseOwnedRevisionChecks()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(browser, "Teacher");
        var content = await LearningTestContent.CreateAsync(fixture, teacher.User.Id);
        var input = new CreateClassroomInput(content.CourseId, "Our class", "A useful description", Guid.NewGuid());
        var created = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Post, "/api/v1/classes", input, teacher.Token),
            browser.SendAsync(HttpMethod.Post, "/api/v1/classes", input, teacher.Token));
        var newlyCreated = Assert.Single(created, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(created, response => response.StatusCode == HttpStatusCode.OK);
        var first = await Read<ClassroomWriteResponse>(created[0]);
        (await Read<ClassroomWriteResponse>(created[1])).Id.Should().Be(first.Id);
        var location = newlyCreated.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal($"/api/v1/classes/{first.Id}", location.AbsolutePath);
        using var locationResponse = await browser.SendAsync(HttpMethod.Get, location.AbsolutePath, bearer: teacher.Token);
        Assert.Equal(HttpStatusCode.OK, locationResponse.StatusCode);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/classes", input with { Name = "Different" }, teacher.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var path = "/api/v1/classes/" + first.Id;
        var updated = await browser.SendAsync(HttpMethod.Patch, path, new EditClassroomInput("Updated", "New description", false, 1), teacher.Token);
        updated.EnsureSuccessStatusCode();
        (await browser.SendAsync(HttpMethod.Patch, path, new EditClassroomInput("Stale", "", false, 1), teacher.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var replay = await Read<ClassroomWriteResponse>(await browser.SendAsync(HttpMethod.Post, "/api/v1/classes", input, teacher.Token));
        replay.Id.Should().Be(first.Id);
        using var otherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var other = await Account(otherBrowser, "Teacher");
        (await otherBrowser.SendAsync(HttpMethod.Get, path, bearer: other.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherBrowser.SendAsync(HttpMethod.Patch, path, new EditClassroomInput("Intruder", "", false, 2), other.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherBrowser.SendAsync(HttpMethod.Post, path + "/invitations", new { days = 1, maxUses = 5 }, other.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherBrowser.SendAsync(HttpMethod.Delete, path + "/members/" + teacher.User.Id, bearer: other.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherBrowser.SendAsync(HttpMethod.Patch, path + "/members/" + teacher.User.Id, new { status = "Active" }, other.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await fixture.Client.GetAsync("/api/v1/classes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LastInvitationUseIsAtomicAndMemberViewsNeverExposeTheRosterOrCodes()
    {
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(teacherBrowser, "Teacher");
        var classroom = await Create(teacherBrowser, teacher.Token, teacher.User.Id);
        var invitation = await Invite(teacherBrowser, teacher.Token, classroom.Id, 1);
        using var oneBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var twoBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var one = await Account(oneBrowser);
        var two = await Account(twoBrowser);
        var responses = await Task.WhenAll(Join(oneBrowser, one.Token, invitation.Code), Join(twoBrowser, two.Token, invitation.Code));
        responses.Select(item => item.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        int winner = responses[0].IsSuccessStatusCode ? 0 : 1;
        var memberBrowser = winner == 0 ? oneBrowser : twoBrowser;
        var member = winner == 0 ? one : two;
        var outsiderBrowser = winner == 0 ? twoBrowser : oneBrowser;
        var outsider = winner == 0 ? two : one;
        (await Join(memberBrowser, member.Token, invitation.Code)).EnsureSuccessStatusCode();
        var response = await memberBrowser.SendAsync(HttpMethod.Get, "/api/v1/classes/" + classroom.Id, bearer: member.Token);
        var view = await Read<ClassroomDetail>(response);
        view.Members.Should().BeNull();
        view.Invitation.Should().BeNull();
        view.Classroom.CanLeave.Should().BeTrue();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("codeHash").And.NotContain(invitation.Code).And.NotContain("email");
        (await outsiderBrowser.SendAsync(HttpMethod.Get, "/api/v1/classes/" + classroom.Id, bearer: outsider.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var hidden = await Read<ClassPage<ClassroomView>>(await outsiderBrowser.SendAsync(HttpMethod.Get, "/api/v1/classes", bearer: outsider.Token));
        hidden.Items.Should().NotContain(item => item.Id == classroom.Id);
        (await outsiderBrowser.SendAsync(HttpMethod.Delete, $"/api/v1/classes/{classroom.Id}/members/{member.User.Id}", bearer: outsider.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsiderBrowser.SendAsync(HttpMethod.Patch, $"/api/v1/classes/{classroom.Id}/members/{member.User.Id}", new { status = "Active" }, outsider.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var ownerView = await Read<ClassroomDetail>(await teacherBrowser.SendAsync(HttpMethod.Get, "/api/v1/classes/" + classroom.Id, bearer: teacher.Token));
        ownerView.Members!.Items.Should().ContainSingle(item => item.UserId == member.User.Id);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var storedInvitation = await database.ClassInvitations.SingleAsync(item => item.ClassroomId == classroom.Id);
        storedInvitation.UsedCount.Should().Be(1);
        storedInvitation.CodeHash.Should().HaveLength(64).And.NotBe(invitation.Code);
        var overflow = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"ClassInvitations\" SET \"UsedCount\" = 2 WHERE \"Id\" = {storedInvitation.Id}"));
        overflow.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        var invalidClass = Guid.NewGuid();
        var foreign = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"ClassMemberships\" (\"ClassroomId\", \"UserId\", \"JoinedAt\", \"Status\") VALUES ({invalidClass}, {member.User.Id}, {DateTimeOffset.UtcNow}, 'Active')"));
        foreign.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        (await database.Enrollments.AnyAsync(item => item.UserId == member.User.Id && item.CourseId == classroom.CourseId)).Should().BeTrue();
    }

    [Fact]
    public async Task RotationExpiryArchiveAndLeavingPreserveIndependentCourseEnrollment()
    {
        var clock = new Clock();
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateBillingClient(clock));
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateBillingClient(clock));
        var teacher = await Account(teacherBrowser, "Teacher");
        var learner = await Account(learnerBrowser);
        var classroom = await Create(teacherBrowser, teacher.Token, teacher.User.Id);
        var old = await Invite(teacherBrowser, teacher.Token, classroom.Id);
        var current = await Invite(teacherBrowser, teacher.Token, classroom.Id);
        (await Join(learnerBrowser, learner.Token, old.Code)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        clock.Now = current.ExpiresAt;
        (await Join(learnerBrowser, learner.Token, current.Code)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        clock.Now = DateTimeOffset.UtcNow;
        current = await Invite(teacherBrowser, teacher.Token, classroom.Id);
        (await Join(learnerBrowser, learner.Token, current.Code)).EnsureSuccessStatusCode();
        (await teacherBrowser.SendAsync(HttpMethod.Patch, "/api/v1/classes/" + classroom.Id,
            new EditClassroomInput("Archived class", "Preserve history", true, 1), teacher.Token)).EnsureSuccessStatusCode();
        (await teacherBrowser.SendAsync(HttpMethod.Post, $"/api/v1/classes/{classroom.Id}/invitations", new { days = 1, maxUses = 10 }, teacher.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var detail = await Read<ClassroomDetail>(await learnerBrowser.SendAsync(HttpMethod.Get, "/api/v1/classes/" + classroom.Id, bearer: learner.Token));
        detail.Classroom.IsArchived.Should().BeTrue();
        (await learnerBrowser.SendAsync(HttpMethod.Delete, $"/api/v1/classes/{classroom.Id}/members/{learner.User.Id}", bearer: learner.Token)).EnsureSuccessStatusCode();
        (await learnerBrowser.SendAsync(HttpMethod.Get, "/api/v1/classes/" + classroom.Id, bearer: learner.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await db.Enrollments.AnyAsync(item => item.UserId == learner.User.Id && item.CourseId == classroom.CourseId)).Should().BeTrue();
    }

    [Fact]
    public async Task RemovedMembershipNeedsTeacherUndoAndLearnerDepartureCannotBeUndone()
    {
        using var teacherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var learnerBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(teacherBrowser, "Teacher");
        var learner = await Account(learnerBrowser);
        var classroom = await Create(teacherBrowser, teacher.Token, teacher.User.Id);
        var invite = await Invite(teacherBrowser, teacher.Token, classroom.Id);
        (await Join(learnerBrowser, learner.Token, invite.Code)).EnsureSuccessStatusCode();
        var path = $"/api/v1/classes/{classroom.Id}/members/{learner.User.Id}";
        (await teacherBrowser.SendAsync(HttpMethod.Delete, path, bearer: teacher.Token)).EnsureSuccessStatusCode();
        (await Join(learnerBrowser, learner.Token, invite.Code)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await learnerBrowser.SendAsync(HttpMethod.Get, "/api/v1/classes/" + classroom.Id, bearer: learner.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await teacherBrowser.SendAsync(HttpMethod.Patch, path, new { status = "Active" }, teacher.Token)).EnsureSuccessStatusCode();
        (await learnerBrowser.SendAsync(HttpMethod.Get, "/api/v1/classes/" + classroom.Id, bearer: learner.Token)).EnsureSuccessStatusCode();
        (await learnerBrowser.SendAsync(HttpMethod.Delete, path, bearer: learner.Token)).EnsureSuccessStatusCode();
        (await teacherBrowser.SendAsync(HttpMethod.Patch, path, new { status = "Active" }, teacher.Token)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Join(learnerBrowser, learner.Token, invite.Code)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TeacherMfaFlagsAndUnknownInputAreRecheckedWithoutLeakingOtherClasses()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var teacher = await Account(browser, "Teacher");
        var classroom = await Create(browser, teacher.Token, teacher.User.Id);
        (await browser.SendAsync(HttpMethod.Patch, "/api/v1/classes/" + classroom.Id,
            new { name = "No overposting", description = "", isArchived = false, revision = 1, teacherId = Guid.NewGuid() }, teacher.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await db.RefreshSessions.Where(item => item.UserId == teacher.User.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.MfaVerified, false));
            var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"ClassMemberships\" (\"ClassroomId\", \"UserId\", \"JoinedAt\", \"Status\") VALUES ({classroom.Id}, {teacher.User.Id}, {DateTimeOffset.UtcNow}, 'Unknown')"));
            invalid.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        }
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/classes/" + classroom.Id, bearer: teacher.Token)).StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        (await browser.SendAsync(HttpMethod.Post, $"/api/v1/classes/{classroom.Id}/invitations", new { days = 1, maxUses = 10 }, teacher.Token)).StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    private sealed record ClassroomWriteResponse(Guid Id);
}

