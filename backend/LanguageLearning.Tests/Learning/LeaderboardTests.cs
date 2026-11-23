using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;

namespace LanguageLearning.Tests.Learning;

public sealed class LeaderboardTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private static DateOnly Monday => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(((int)DateTime.UtcNow.DayOfWeek + 6) % 7));
    private static DateTimeOffset Start(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    private static void Award(LexiGoDbContext db, Guid user, Guid course, int xp, DateTimeOffset at) =>
        db.XpTransactions.Add(new() { UserId = user, CourseId = course, SourceType = "LeaderboardFixture", SourceId = Guid.NewGuid(), Amount = xp, EarnedAt = at });

    private static Guid Participant(LexiGoDbContext db, Guid course, string name, int xp, DateTimeOffset at, bool optedIn = true, bool confirmed = true)
    {
        // Synthetic participants in this test's disposable PostgreSQL container only; no sign-in credentials.
        Guid id = Guid.NewGuid();
        string email = $"ranking-{id:N}@example.test";
        db.Users.Add(new ApplicationUser { Id = id, DisplayName = name, UserName = email, NormalizedUserName = email.ToUpperInvariant(), Email = email, NormalizedEmail = email.ToUpperInvariant(), EmailConfirmed = confirmed });
        db.UserPreferences.Add(new UserPreference { UserId = id, LeaderboardOptIn = optedIn });
        db.Enrollments.Add(new Enrollment { UserId = id, CourseId = course, EnrolledAt = at });
        Award(db, id, course, xp, at);
        return id;
    }

    private async Task<(SecurityTestAccount User, string Token, Guid Course)> SetupAsync(SessionTestBrowser browser)
    {
        var user = await SecurityTestAccount.CreateAsync(fixture);
        string token = await user.LoginAsync(browser);
        var content = await LearningTestContent.CreateAsync(fixture, user.Id);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/me/enrollments", new { content.CourseId }, token)).EnsureSuccessStatusCode();
        return (user, token, content.CourseId);
    }

    [Fact]
    public async Task RankingNameReportsRequireAVisibleUnchangedNameAndModeratorResolutionHidesIt()
    {
        const string reports = "/api/v1/reports/ranking-names";
        using var reporter = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(reporter);
        Guid target;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            target = Participant(db, state.Course, "Reported Learner", 50000, Start(Monday).AddHours(1));
            await db.SaveChangesAsync();
        }
        using var initialBoard = await reporter.SendAsync(HttpMethod.Get,
            $"/api/v1/leaderboards?courseId={state.Course}", bearer: state.Token);
        var row = (await initialBoard.Content.ReadFromJsonAsync<LeaderboardView>())!.Items
            .Single(item => item.DisplayName == "Reported Learner");
        var input = new CreateRankingNameReport(state.Course, Monday, "Reported Learner",
            "Review this name.", row.ReportToken);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await reporter.SendAsync(HttpMethod.Post, reports, input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await reporter.SendAsync(HttpMethod.Post, reports, input with { DisplayName = "Stale name" }, state.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await reporter.SendAsync(HttpMethod.Post, reports, input with { CourseId = Guid.NewGuid() }, state.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await reporter.SendAsync(HttpMethod.Post, reports, input with { ReportToken = "tampered" }, state.Token)).StatusCode);
        using var created = await reporter.SendAsync(HttpMethod.Post, reports, input, state.Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var report = (await created.Content.ReadFromJsonAsync<ContentReportView>())!;
        Assert.Equal(Guid.Empty, report.ResourceId);
        using var export = await reporter.SendAsync(HttpMethod.Get, "/api/v1/me/export", bearer: state.Token);
        export.EnsureSuccessStatusCode();
        var exportedJson = await export.Content.ReadAsStringAsync();
        Assert.DoesNotContain(target.ToString(), exportedJson);
        Assert.Contains("Reported display name: Reported Learner", exportedJson);
        Assert.Equal(HttpStatusCode.OK,
            (await reporter.SendAsync(HttpMethod.Post, reports, input, state.Token)).StatusCode);
        using var moderator = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var account = await SecurityTestAccount.CreateAsync(fixture);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(account.Id.ToString()))!,
                RoleNames.Moderator)).Succeeded);
        }
        string staffToken = await account.LoginAsync(moderator);
        var path = $"/api/v1/admin/reports/{report.Id}";
        var decision = new ReviewContentReport("Resolved", "Ranking name reviewed.");
        Assert.Equal(HttpStatusCode.NotFound,
            (await reporter.SendAsync(HttpMethod.Patch, path, decision, state.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await moderator.SendAsync(HttpMethod.Patch, path, decision, staffToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await moderator.SendAsync(HttpMethod.Patch, path, decision, staffToken)).StatusCode);
        using var response = await reporter.SendAsync(HttpMethod.Get,
            $"/api/v1/leaderboards?courseId={state.Course}", bearer: state.Token);
        Assert.DoesNotContain((await response.Content.ReadFromJsonAsync<LeaderboardView>())!.Items,
            item => item.DisplayName == "Reported Learner");
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var final = finalScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(1, await final.Notifications.CountAsync(item => item.UserId == target
            && item.Title == "Your ranking participation was paused"));
        Assert.Equal(50000, await final.XpTransactions.Where(item => item.UserId == target).SumAsync(item => item.Amount));
    }

    [Fact]
    public async Task RankingUsesCourseWeekAndTimeAttainedWithoutPublishingPrivateData()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(browser);
        DateOnly week = Monday.AddDays(-14);
        var start = Start(week);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            Participant(db, state.Course, "Higher score", 60, start.AddHours(5));
            Participant(db, state.Course, "Earlier tie", 40, start.AddHours(2));
            Participant(db, state.Course, "Private participant", 1000, start.AddHours(1), optedIn: false);
            Participant(db, state.Course, "Unconfirmed participant", 1000, start.AddHours(1), confirmed: false);
            Award(db, state.User.Id, state.Course, 20, start.AddHours(1));
            Award(db, state.User.Id, state.Course, 20, start.AddHours(3));
            Award(db, state.User.Id, state.Course, 1000, start.AddTicks(-1));
            Award(db, state.User.Id, state.Course, 1000, start.AddDays(7));
            var preference = await db.UserPreferences.SingleAsync(item => item.UserId == state.User.Id);
            preference.LeaderboardOptIn = true;
            await db.SaveChangesAsync();
        }
        var response = await browser.SendAsync(HttpMethod.Get, $"/api/v1/leaderboards?courseId={state.Course}&week={week:yyyy-MM-dd}", bearer: state.Token);
        response.EnsureSuccessStatusCode();
        var value = (await response.Content.ReadFromJsonAsync<LeaderboardView>())!;
        value.TotalCount.Should().Be(3);
        value.Items.Select(item => item.Xp).Should().Equal(60, 40, 40);
        value.Items[1].DisplayName.Should().Be("Earlier tie");
        value.Me!.Rank.Should().Be(3);
        value.MyXp.Should().Be(40);
        value.Items.Count(item => item.IsMe).Should().Be(1);
        string json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("@example.test").And.NotContain("Private participant").And.NotContain("Unconfirmed participant").And.NotContain("achievedAt").And.NotContain("sourceId").And.NotContain("userId");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await fixture.Client.GetAsync($"/api/v1/leaderboards?courseId={state.Course}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await browser.SendAsync(HttpMethod.Get, $"/api/v1/leaderboards?courseId={Guid.NewGuid()}", bearer: state.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await browser.SendAsync(HttpMethod.Get, $"/api/v1/leaderboards?courseId={state.Course}&week={week.AddDays(1):yyyy-MM-dd}", bearer: state.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Get, $"/api/v1/leaderboards?courseId={state.Course}&week={Monday.AddDays(7):yyyy-MM-dd}", bearer: state.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Get, $"/api/v1/leaderboards?courseId={state.Course}&page=0", bearer: state.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var archivedScope = fixture.Services.CreateAsyncScope();
        var archivedDb = archivedScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await archivedDb.Courses.SingleAsync(item => item.Id == state.Course)).IsPublished = false;
        await archivedDb.SaveChangesAsync();
        (await browser.SendAsync(HttpMethod.Get, $"/api/v1/leaderboards?courseId={state.Course}&week={week:yyyy-MM-dd}", bearer: state.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ParticipationIsExplicitAndPreservesOtherPreferencesAndAccounts()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(browser);
        Guid other;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            other = Participant(db, state.Course, "Another learner", 20, Start(Monday).AddHours(1));
            Award(db, state.User.Id, state.Course, 20, Start(Monday).AddHours(2));
            var preference = await db.UserPreferences.SingleAsync(item => item.UserId == state.User.Id);
            preference.DailyGoalXp = 45;
            preference.TimeZone = "Asia/Tbilisi";
            preference.ReduceMotion = true;
            await db.SaveChangesAsync();
        }
        async Task<LeaderboardView> Read() => (await (await browser.SendAsync(HttpMethod.Get,
            $"/api/v1/leaderboards?courseId={state.Course}", bearer: state.Token)).Content.ReadFromJsonAsync<LeaderboardView>())!;
        (await Read()).Me.Should().BeNull();
        (await Read()).MyXp.Should().Be(20);
        foreach (bool optedIn in new[] { true, false })
        {
            (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/leaderboard-participation", new { optedIn }, state.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            var value = await Read();
            value.OptedIn.Should().Be(optedIn);
            (value.Me is not null).Should().Be(optedIn);
        }
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/leaderboard-participation", new { }, state.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Put, "/api/v1/me/leaderboard-participation", new { optedIn = true, userId = other }, state.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var finalScope = fixture.Services.CreateAsyncScope();
        var final = finalScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var saved = await final.UserPreferences.SingleAsync(item => item.UserId == state.User.Id);
        saved.DailyGoalXp.Should().Be(45);
        saved.TimeZone.Should().Be("Asia/Tbilisi");
        saved.ReduceMotion.Should().BeTrue();
        (await final.UserPreferences.SingleAsync(item => item.UserId == other)).LeaderboardOptIn.Should().BeTrue();
    }

    [Fact]
    public async Task PhotoConsentIsExplicitAndIsClearedByLeavingOrRemovingThePhoto()
    {
        const string participation = "/api/v1/me/leaderboard-participation";
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(browser);
        Guid avatarId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var avatar = new MediaAsset
            {
                UploadedBy = state.User.Id, StorageKey = $"test-avatar/{Guid.NewGuid():N}",
                MimeType = "image/png", ByteSize = 128, Source = "LexiGo profile avatar", License = "Private",
            };
            db.MediaAssets.Add(avatar);
            var user = await db.Users.SingleAsync(item => item.Id == state.User.Id);
            user.AvatarMediaId = avatar.Id;
            await db.SaveChangesAsync();
            avatarId = avatar.Id;
        }

        async Task<Guid?> ReadConsent()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().UserPreferences
                .Where(item => item.UserId == state.User.Id)
                .Select(item => item.SharedLeaderboardAvatarId).SingleAsync();
        }

        (await browser.SendAsync(HttpMethod.Put, participation, new { optedIn = false, sharePhoto = true }, state.Token))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.SendAsync(HttpMethod.Put, participation, new { optedIn = true }, state.Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadConsent()).Should().BeNull();
        (await browser.SendAsync(HttpMethod.Put, participation, new { optedIn = true, sharePhoto = true }, state.Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadConsent()).Should().Be(avatarId);
        (await browser.SendAsync(HttpMethod.Put, participation, new { optedIn = false }, state.Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadConsent()).Should().BeNull();

        (await browser.SendAsync(HttpMethod.Put, participation, new { optedIn = true, sharePhoto = true }, state.Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await browser.SendAsync(HttpMethod.Delete, "/api/v1/me/avatar", bearer: state.Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadConsent()).Should().BeNull();
    }

    [Fact]
    public async Task PagesAreBoundedAndOwnRankIsAvailableOutsideTheCurrentPage()
    {
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var state = await SetupAsync(browser);
        DateOnly week = Monday.AddDays(-70);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            for (int index = 1; index <= 25; index++)
                Participant(db, state.Course, $"Participant {index}", 100 + index, Start(week).AddHours(1));
            Award(db, state.User.Id, state.Course, 1, Start(week).AddHours(1));
            (await db.UserPreferences.SingleAsync(item => item.UserId == state.User.Id)).LeaderboardOptIn = true;
            await db.SaveChangesAsync();
        }
        var response = await browser.SendAsync(HttpMethod.Get, $"/api/v1/leaderboards?courseId={state.Course}&week={week:yyyy-MM-dd}", bearer: state.Token);
        response.EnsureSuccessStatusCode();
        var first = (await response.Content.ReadFromJsonAsync<LeaderboardView>())!;
        first.Items.Should().HaveCount(20);
        first.TotalCount.Should().Be(26);
        first.Me!.Rank.Should().Be(26);
        var secondResponse = await browser.SendAsync(HttpMethod.Get, $"/api/v1/leaderboards?courseId={state.Course}&week={week:yyyy-MM-dd}&page=2", bearer: state.Token);
        var second = (await secondResponse.Content.ReadFromJsonAsync<LeaderboardView>())!;
        second.Items.Should().HaveCount(6);
        first.Items.Select(item => item.Rank).Intersect(second.Items.Select(item => item.Rank)).Should().BeEmpty();
    }
}
