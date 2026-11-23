using System.Net;
using System.Net.Http.Json;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class DialogueSessionTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task ChoiceFollowsSavedBranchAndReplayDoesNotDuplicateThePath()
    {
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await learner.LoginAsync(browser);
        var outsider = await SecurityTestAccount.CreateAsync(fixture);
        using var other = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string otherToken = await outsider.LoginAsync(other);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid courseId = (await LearningTestContent.CreateAsync(fixture, learner.Id)).CourseId;
        var dialogue = new Dialogue { CourseId = courseId, Title = "Café", Scenario = "cafe", Status = "Published" };
        var start = new DialogueNode { DialogueId = dialogue.Id, Text = "What would you like?", IsStart = true };
        var tea = new DialogueNode { DialogueId = dialogue.Id, Text = "Here is your tea.", IsTerminal = true };
        var coffee = new DialogueNode { DialogueId = dialogue.Id, Text = "Here is your coffee.", IsTerminal = true };
        var choice = new DialogueChoice
        {
            DialogueId = dialogue.Id, NodeId = start.Id, NextNodeId = tea.Id,
            Text = "Tea, please.", Feedback = "Please makes this request polite.", IsCorrect = true
        };
        var incorrect = new DialogueChoice
        {
            DialogueId = dialogue.Id, NodeId = start.Id, NextNodeId = coffee.Id,
            Text = "Coffee.", Feedback = "Try adding please."
        };
        db.AddRange(dialogue, start, tea, coffee, choice, incorrect);
        await db.SaveChangesAsync();

        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId }, token)).EnsureSuccessStatusCode();
        await db.UserPreferences.Where(item => item.UserId == learner.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.DailyGoalXp, 10));
        var list = await browser.SendAsync(HttpMethod.Get, $"/api/v1/dialogues?courseId={courseId}", bearer: token);
        Assert.Contains((await list.Content.ReadFromJsonAsync<DialogueListing[]>())!, item => item.Id == dialogue.Id);
        string startRoute = $"/api/v1/dialogues/{dialogue.Id}/sessions";
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Post, startRoute, bearer: otherToken)).StatusCode);
        var starts = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Post, startRoute, bearer: token),
            browser.SendAsync(HttpMethod.Post, startRoute, bearer: token));
        using var created = Assert.Single(starts, response => response.StatusCode == HttpStatusCode.Created);
        using var resumedResponse = Assert.Single(starts, response => response.StatusCode == HttpStatusCode.OK);
        string initial = await created.Content.ReadAsStringAsync();
        var session = await ReadAsync(created);
        Assert.Equal("es", session.LanguageCode);
        Assert.Equal(session.Id, (await ReadAsync(resumedResponse)).Id);
        Assert.DoesNotContain("nextNodeId", initial);
        Assert.DoesNotContain(choice.Feedback, initial);
        Assert.DoesNotContain(tea.Text, initial);
        Assert.Empty(session.ReviewPhrases);

        Assert.NotNull(created.Headers.Location);
        string route = created.Headers.Location.AbsolutePath;
        Assert.Equal($"/api/v1/me/dialogue-sessions/{session.Id}", route);
        using var detail = await browser.SendAsync(HttpMethod.Get, route, bearer: token);
        Assert.Equal(session.Id, (await ReadAsync(detail)).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Get, route, bearer: otherToken)).StatusCode);
        var request = new { choiceId = choice.Id, idempotencyKey = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Post, route + "/choices", request, otherToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, route + "/choices",
            request with { choiceId = Guid.NewGuid() }, token)).StatusCode);
        await db.DialogueChoices.Where(item => item.Id == choice.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.NextNodeId, coffee.Id));
        var choices = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Post, route + "/choices", request, token),
            browser.SendAsync(HttpMethod.Post, route + "/choices", request, token));
        foreach (var response in choices)
        {
            var complete = await ReadAsync(response);
            Assert.Equal(tea.Id, complete.Current.Id);
            Assert.NotNull(complete.CompletedAt);
            Assert.Equal(10, complete.AwardedXp);
            Assert.Equal(choice.Feedback, Assert.Single(complete.Path).Feedback);
            Assert.Equal(new DialogueReviewPhrase(choice.Text, choice.Feedback), Assert.Single(complete.ReviewPhrases));
        }
        Assert.Single(await db.DialogueAttempts.Where(item => item.SessionId == session.Id).ToArrayAsync());
        Assert.Single((await ReadAsync(await browser.SendAsync(HttpMethod.Get, route, bearer: token))).Path);
        Assert.Equal(10, (await ReadAsync(await browser.SendAsync(HttpMethod.Get, route, bearer: token))).AwardedXp);
        var activity = await db.DailyActivities.AsNoTracking().SingleAsync(item => item.UserId == learner.Id);
        Assert.Equal(10, activity.EarnedXp);
        Assert.Equal(0, activity.CompletedLessons);
        Assert.True(activity.GoalReached);
        Assert.Equal(1, await db.XpTransactions.CountAsync(item => item.UserId == learner.Id
            && item.SourceType == "FirstDialogueCompletion"));
        await db.DialogueChoices.Where(item => item.Id == choice.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.NextNodeId, tea.Id));
        var second = await ReadAsync(await browser.SendAsync(HttpMethod.Post, startRoute, bearer: token));
        var replacement = new Dialogue
        {
            CourseId = courseId, Title = "Café, revised", Scenario = dialogue.Scenario,
            VersionNumber = 2, Status = "Published"
        };
        var newStart = new DialogueNode { DialogueId = replacement.Id, Text = "What can I get you?", IsStart = true };
        var newEnd = new DialogueNode { DialogueId = replacement.Id, Text = "Of course.", IsTerminal = true };
        db.AddRange(replacement, newStart, newEnd, new DialogueChoice
        {
            DialogueId = replacement.Id, NodeId = newStart.Id, NextNodeId = newEnd.Id,
            Text = "Tea, please.", Feedback = "A polite request.", IsCorrect = true
        });
        await db.SaveChangesAsync();
        var updatedList = await browser.SendAsync(HttpMethod.Get, $"/api/v1/dialogues?courseId={courseId}", bearer: token);
        var listing = Assert.Single((await updatedList.Content.ReadFromJsonAsync<DialogueListing[]>())!);
        Assert.Equal(replacement.Id, listing.Id);
        Assert.Equal(second.Id, listing.ResumeSessionId);
        Assert.True(listing.HasCompleted);
        (await other.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { courseId }, otherToken)).EnsureSuccessStatusCode();
        var otherList = await other.SendAsync(HttpMethod.Get, $"/api/v1/dialogues?courseId={courseId}", bearer: otherToken);
        var otherListing = Assert.Single((await otherList.Content.ReadFromJsonAsync<DialogueListing[]>())!);
        Assert.False(otherListing.HasCompleted);
        Assert.Null(otherListing.ResumeSessionId);
        var resumed = await ReadAsync(await browser.SendAsync(HttpMethod.Post,
            $"/api/v1/dialogues/{replacement.Id}/sessions", bearer: token));
        Assert.Equal(second.Id, resumed.Id);
        Assert.Equal(dialogue.Id, resumed.DialogueId);
        var corrected = await ReadAsync(await browser.SendAsync(HttpMethod.Post,
            $"/api/v1/me/dialogue-sessions/{second.Id}/choices",
            new { choiceId = incorrect.Id, idempotencyKey = Guid.NewGuid() }, token));
        Assert.Equal(coffee.Id, corrected.Current.Id);
        Assert.False(Assert.Single(corrected.Path).IsCorrect);
        Assert.Equal(0, corrected.AwardedXp);
        Assert.Equal(new DialogueReviewPhrase(choice.Text, choice.Feedback), Assert.Single(corrected.ReviewPhrases));
        var replayed = await ReadAsync(await browser.SendAsync(HttpMethod.Post, startRoute, bearer: token));
        Assert.NotEqual(second.Id, replayed.Id);
        Assert.Equal(replacement.Id, replayed.DialogueId);
        Assert.Equal(newStart.Id, replayed.Current.Id);
        var newChoice = await db.DialogueChoices.SingleAsync(item => item.NodeId == newStart.Id);
        var repeated = await ReadAsync(await browser.SendAsync(HttpMethod.Post,
            $"/api/v1/me/dialogue-sessions/{replayed.Id}/choices",
            new { choiceId = newChoice.Id, idempotencyKey = Guid.NewGuid() }, token));
        Assert.Equal(0, repeated.AwardedXp);
        var otherStart = await ReadAsync(await other.SendAsync(HttpMethod.Post,
            $"/api/v1/dialogues/{replacement.Id}/sessions", bearer: otherToken));
        var otherFinish = await ReadAsync(await other.SendAsync(HttpMethod.Post,
            $"/api/v1/me/dialogue-sessions/{otherStart.Id}/choices",
            new { choiceId = newChoice.Id, idempotencyKey = Guid.NewGuid() }, otherToken));
        Assert.Equal(10, otherFinish.AwardedXp);
        Assert.Equal(10, await db.XpTransactions.Where(item => item.UserId == learner.Id).SumAsync(item => item.Amount));
    }

    private static async Task<DialogueView> ReadAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DialogueView>())!;
    }
}
