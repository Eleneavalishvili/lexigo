using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class RegistrationTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string Password = "A long test passphrase 42!";
    private static string NewEmail() => $"learner-{Guid.NewGuid():N}@example.test";
    private Task<HttpResponseMessage> RegisterAsync(string email, string password = Password) =>
        fixture.Client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = "Curious Learner", password });

    [Fact]
    public async Task RegistrationStoresHashAndOnlyLearnerRole()
    {
        string email = NewEmail();
        var response = await RegisterAsync(email);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        user.PasswordHash.Should().NotBe(Password);
        (await users.CheckPasswordAsync(user, Password)).Should().BeTrue();
        user.EmailConfirmed.Should().BeFalse();
        (await users.GetRolesAsync(user)).Should().Equal(RoleNames.Learner);
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.UserPreferences.AnyAsync(preference => preference.UserId == user.Id)).Should().BeTrue();
        (await database.EmailOutboxMessages.CountAsync(message => message.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task DuplicateEmailHasSamePublicResponseAndNoDuplicateAccount()
    {
        string email = NewEmail();
        var first = await RegisterAsync(email);
        var duplicate = await RegisterAsync(email.ToUpperInvariant());
        duplicate.StatusCode.Should().Be(first.StatusCode);
        (await duplicate.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.Users.CountAsync(user => user.NormalizedEmail == email.ToUpperInvariant())).Should().Be(1);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    [InlineData("lowercase password with symbol!")]
    [InlineData("Uppercase password without symbol")]
    [InlineData("UPPERCASE123456789     ")]
    public async Task WeakPasswordCreatesNeitherAccountNorEmail(string password)
    {
        string email = NewEmail();
        (await RegisterAsync(email, password)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.Users.AnyAsync(user => user.Email == email)).Should().BeFalse();
        (await database.EmailOutboxMessages.AnyAsync(message => message.Recipient == email)).Should().BeFalse();
    }

    [Fact]
    public async Task UnicodeSymbolIsAcceptedAlongsideAnUppercaseLetter()
    {
        (await RegisterAsync(NewEmail(), "A memorable password 🦋")).StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task CannotRegisterAnAdministrativeRoleByOverposting()
    {
        string email = NewEmail();
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, displayName = "Learner", password = Password, roles = new[] { RoleNames.SuperAdmin } });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.Users.AnyAsync(user => user.Email == email)).Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmationIsSingleUse()
    {
        string email = NewEmail();
        await RegisterAsync(email);
        var confirmation = await fixture.ConfirmationAsync(email);
        (await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", confirmation.AsRequest())).StatusCode.Should().Be(HttpStatusCode.OK);
        var repeated = await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", confirmation.AsRequest());
        repeated.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await repeated.Content.ReadAsStringAsync()).Should().Contain("email_already_confirmed");
    }

    [Fact]
    public async Task AlreadyConfirmedNoticeRequiresAValidToken()
    {
        string email = NewEmail();
        await RegisterAsync(email);
        var confirmation = await fixture.ConfirmationAsync(email);
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", confirmation.AsRequest());
        var invalid = await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = confirmation.UserId, token = confirmation.Token + "invalid" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadAsStringAsync()).Should().Contain("invalid_confirmation").And.NotContain("email_already_confirmed");
        var missing = await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = Guid.NewGuid(), token = confirmation.Token });
        (await missing.Content.ReadAsStringAsync()).Should().Contain("invalid_confirmation").And.NotContain("email_already_confirmed");
    }

    [Fact]
    public async Task ExpiredTokenCannotRevealAlreadyConfirmedStatus()
    {
        string email = NewEmail();
        await RegisterAsync(email);
        var confirmation = await fixture.ConfirmationAsync(email);
        await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", confirmation.AsRequest());
        var options = fixture.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataProtectionTokenProviderOptions>>().Value;
        TimeSpan original = options.TokenLifespan;
        try
        {
            options.TokenLifespan = TimeSpan.Zero;
            var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", confirmation.AsRequest());
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_confirmation").And.NotContain("email_already_confirmed");
        }
        finally { options.TokenLifespan = original; }
    }

    [Fact]
    public async Task ConfirmationRejectsTokenOutsideConfiguredLifespan()
    {
        string email = NewEmail();
        await RegisterAsync(email);
        var confirmation = await fixture.ConfirmationAsync(email);
        var options = fixture.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataProtectionTokenProviderOptions>>().Value;
        TimeSpan original = options.TokenLifespan;
        try
        {
            options.TokenLifespan = TimeSpan.Zero;
            var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", confirmation.AsRequest());
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            options.TokenLifespan = original;
        }
    }

    [Fact]
    public async Task TokenCannotConfirmAnotherAccount()
    {
        string first = NewEmail();
        string second = NewEmail();
        await RegisterAsync(first);
        await RegisterAsync(second);
        var original = await fixture.ConfirmationAsync(first);
        var other = await fixture.ConfirmationAsync(second);
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = other.UserId, token = original.Token });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.Users.SingleAsync(user => user.Id == other.UserId)).EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task TamperedTokenIsRejected()
    {
        string email = NewEmail();
        await RegisterAsync(email);
        var confirmation = await fixture.ConfirmationAsync(email);
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = confirmation.UserId, token = confirmation.Token + "invalid" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConcurrentRegistrationsCreateOneAccount()
    {
        string email = NewEmail();
        var responses = await Task.WhenAll(RegisterAsync(email), RegisterAsync(email));
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Accepted);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.Users.CountAsync(user => user.Email == email)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentConfirmationSucceedsOnce()
    {
        string email = NewEmail();
        await RegisterAsync(email);
        var confirmation = await fixture.ConfirmationAsync(email);
        var responses = await Task.WhenAll(
            fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", confirmation.AsRequest()),
            fixture.Client.PostAsJsonAsync("/api/v1/auth/verify-email", confirmation.AsRequest()));
        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest).Should().Be(1);
    }

    [Fact]
    public async Task AllFourCoursesAndSevenRolesAreInitialized()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.Courses.CountAsync()).Should().Be(4);
        (await database.Roles.CountAsync()).Should().Be(7);
        (await database.Courses.Include(course => course.SourceLanguage).Select(course => course.SourceLanguage.Code).Distinct().ToArrayAsync()).Should().Equal("en");
    }

    [Fact]
    public async Task ResendIsGenericAndThrottledPerAccount()
    {
        string email = NewEmail();
        await RegisterAsync(email);
        var known = await fixture.Client.PostAsJsonAsync("/api/v1/auth/resend-verification", new { email });
        var unknown = await fixture.Client.PostAsJsonAsync("/api/v1/auth/resend-verification", new { email = NewEmail() });
        known.StatusCode.Should().Be(HttpStatusCode.Accepted);
        unknown.StatusCode.Should().Be(known.StatusCode);
        (await unknown.Content.ReadAsStringAsync()).Should().Be(await known.Content.ReadAsStringAsync());
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await database.EmailOutboxMessages.CountAsync(message => message.Recipient == email)).Should().Be(1);
    }

    [Fact]
    public async Task ResendAfterCooldownQueuesNewMessage()
    {
        string email = NewEmail();
        await RegisterAsync(email);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var message = await database.EmailOutboxMessages.SingleAsync(message => message.Recipient == email);
            message.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6);
            await database.SaveChangesAsync();
        }
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/resend-verification", new { email });
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await using var checkScope = fixture.Services.CreateAsyncScope();
        var checkDatabase = checkScope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        (await checkDatabase.EmailOutboxMessages.CountAsync(message => message.Recipient == email)).Should().Be(2);
    }

    [Fact]
    public async Task AccountEntryIsRateLimited()
    {
        using var client = fixture.CreateRateLimitedClient();
        var first = await client.PostAsJsonAsync("/api/v1/auth/resend-verification", new { email = NewEmail() });
        var second = await client.PostAsJsonAsync("/api/v1/auth/resend-verification", new { email = NewEmail() });
        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task DatabaseReadinessChecksAnActualSchema()
    {
        (await fixture.Client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

internal static class ConfirmationRequestExtensions
{
    public static object AsRequest(this (Guid UserId, string Token) confirmation) => new { userId = confirmation.UserId, token = confirmation.Token };
}
