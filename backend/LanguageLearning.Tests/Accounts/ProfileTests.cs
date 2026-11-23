using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class ProfileTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private static PreferenceDetails Preferences() => new("Asia/Tbilisi", 35, "dark", true, false, false,
        null, 85, new TimeOnly(19, 30), true);

    private async Task<(SessionTestBrowser Browser, Guid Id, string Token)> LearnerAsync()
    {
        string email = $"profile-{Guid.NewGuid():N}@example.test";
        const string password = "Profile test passphrase 42!";
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = "Curious Learner", password })).EnsureSuccessStatusCode();
        var confirmation = await fixture.ConfirmationAsync(email);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = confirmation.UserId, token = confirmation.Token })).EnsureSuccessStatusCode();
        var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var login = await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password }, csrf: true);
        login.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return (browser, confirmation.UserId, document.RootElement.GetProperty("accessToken").GetString()!);
    }

    [Fact]
    public async Task PreferencesPersistWithoutChangingAnotherAccount()
    {
        var learner = await LearnerAsync();
        var other = await LearnerAsync();
        using var browser = learner.Browser;
        using var otherBrowser = other.Browser;
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", Preferences(), learner.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var saved = await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/preferences", bearer: learner.Token)).Content.ReadFromJsonAsync<PreferenceDetails>();
        saved.Should().Be(Preferences());
        var unchanged = await (await otherBrowser.SendAsync(HttpMethod.Get, "/api/v1/me/preferences", bearer: other.Token)).Content.ReadFromJsonAsync<PreferenceDetails>();
        unchanged!.TimeZone.Should().Be("UTC");
        unchanged.EmailRemindersEnabled.Should().BeFalse();
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", new { userId = other.Id, timeZone = "UTC" }, learner.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("timeZone")]
    [InlineData("dailyGoalXp")]
    [InlineData("theme")]
    [InlineData("studyDays")]
    [InlineData("reminderTime")]
    [InlineData("activeCourseId")]
    public async Task InvalidPreferencesAreRejectedWithoutPartialWrites(string field)
    {
        var learner = await LearnerAsync();
        using var browser = learner.Browser;
        var valid = Preferences();
        var invalid = field switch
        {
            "timeZone" => valid with { TimeZone = "Imaginary/Place" },
            "dailyGoalXp" => valid with { DailyGoalXp = 1001 },
            "theme" => valid with { Theme = "<script>" },
            "studyDays" => valid with { StudyDays = 0 },
            "reminderTime" => valid with { ReminderTime = new TimeOnly(19, 30, 1) },
            _ => valid with { ActiveCourseId = Guid.NewGuid() }
        };
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", invalid, learner.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var saved = await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/preferences", bearer: learner.Token)).Content.ReadFromJsonAsync<PreferenceDetails>();
        saved!.DailyGoalXp.Should().Be(20);
        saved.Theme.Should().Be("light");
    }

    [Fact]
    public async Task CourseSelectionRequiresPublishedCourse()
    {
        var learner = await LearnerAsync();
        using var browser = learner.Browser;
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var course = await database.Courses.OrderBy(item => item.OrderNumber).FirstAsync();
        var value = Preferences() with { ActiveCourseId = course.Id };
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", value, learner.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        course.IsPublished = true;
        await database.SaveChangesAsync();
        try
        {
            (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", value, learner.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            var choices = await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/preferences/courses", bearer: learner.Token)).Content.ReadFromJsonAsync<CourseChoice[]>();
            choices.Should().ContainSingle(item => item.Id == course.Id);
        }
        finally { course.IsPublished = false; await database.SaveChangesAsync(); }
    }

    [Fact]
    public async Task RenameIsOwnedAndCannotChangeRolesOrEmail()
    {
        var learner = await LearnerAsync();
        using var browser = learner.Browser;
        using var renamed = await browser.SendAsync(HttpMethod.Patch, "/api/v1/me",
            new { displayName = "  A new hello  " }, learner.Token);
        renamed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await renamed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("displayName").GetString()
            .Should().Be("A new hello");
        (await browser.SendAsync(HttpMethod.Patch, "/api/v1/me", new { displayName = "Wrong", roles = new[] { "SuperAdmin" } }, learner.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Patch, "/api/v1/me", new { displayName = "\n\t" }, learner.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var other = await LearnerAsync();
        using var otherBrowser = other.Browser;
        using var current = await browser.SendAsync(HttpMethod.Get, $"/api/v1/me?userId={other.Id}", bearer: learner.Token);
        current.EnsureSuccessStatusCode();
        current.Headers.CacheControl!.NoStore.Should().BeTrue();
        var summary = (await current.Content.ReadFromJsonAsync<AccountSummary>())!;
        summary.Id.Should().Be(learner.Id);
        summary.DisplayName.Should().Be("A new hello");
        var otherSummary = await (await otherBrowser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: other.Token))
            .Content.ReadFromJsonAsync<AccountSummary>();
        otherSummary!.DisplayName.Should().Be("Curious Learner");
        await using var scope = fixture.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().Users.SingleAsync(item => item.Id == learner.Id)).DisplayName.Should().Be("A new hello");
    }

    [Fact]
    public async Task ConcurrentUpdatesStayWholeAndConstraintsRejectInvalidDatabaseWrites()
    {
        var learner = await LearnerAsync();
        using var browser = learner.Browser;
        using var second = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var firstValue = Preferences();
        var secondValue = Preferences() with { Theme = "light", DailyGoalXp = 50, StudyDays = 127 };
        var responses = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", firstValue, learner.Token),
            second.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", secondValue, learner.Token));
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.NoContent);
        var saved = await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/preferences", bearer: learner.Token)).Content.ReadFromJsonAsync<PreferenceDetails>();
        (saved == firstValue || saved == secondValue).Should().BeTrue();
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var preference = await database.UserPreferences.SingleAsync(item => item.UserId == learner.Id);
        preference.StudyDays = 0;
        await FluentActions.Invoking(() => database.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task AnonymousRequestsCannotReadOrChangePreferences()
    {
        (await fixture.Client.GetAsync("/api/v1/me/preferences")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fixture.Client.PutAsJsonAsync("/api/v1/me/preferences", Preferences())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fixture.Client.PatchAsJsonAsync("/api/v1/me", new { displayName = "Intruder" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OnboardingGoalChangesOnlyTheCurrentLearnersDailyXpTarget()
    {
        var learner = await LearnerAsync();
        var other = await LearnerAsync();
        using var browser = learner.Browser;
        using var otherBrowser = other.Browser;
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", Preferences(), learner.Token)).EnsureSuccessStatusCode();
        var beforeOther = await (await otherBrowser.SendAsync(HttpMethod.Get, "/api/v1/me/preferences", bearer: other.Token)).Content.ReadFromJsonAsync<PreferenceDetails>();
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/study-goal", new { dailyGoalXp = 40 }, learner.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var saved = await (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/preferences", bearer: learner.Token)).Content.ReadFromJsonAsync<PreferenceDetails>();
        saved.Should().Be(Preferences() with { DailyGoalXp = 40 });
        var afterOther = await (await otherBrowser.SendAsync(HttpMethod.Get, "/api/v1/me/preferences", bearer: other.Token)).Content.ReadFromJsonAsync<PreferenceDetails>();
        afterOther.Should().Be(beforeOther);
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/study-goal", new { dailyGoalXp = 0 }, learner.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/study-goal", new { dailyGoalXp = 80, userId = other.Id }, learner.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fixture.Client.PutAsJsonAsync("/api/v1/me/study-goal", new { dailyGoalXp = 40 })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
