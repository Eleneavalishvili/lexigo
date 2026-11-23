using FluentAssertions;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class SuperAdminBootstrapTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Each case owns an isolated fixture database; reset only this test's bootstrap state.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid role = await database.Roles.Where(item => item.NormalizedName == "SUPERADMIN").Select(item => item.Id).SingleAsync();
        await database.UserRoles.Where(item => item.RoleId == role).ExecuteDeleteAsync();
        await database.AuditLogs.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<bool> PromoteAsync(SecurityTestAccount account, string? password = null, string? code = null)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SuperAdminBootstrapService>().PromoteAsync(
            account.Email, password ?? SecurityTestAccount.Password, code ?? TestAuthenticator.Code(account.Key), CancellationToken.None);
    }

    [Fact]
    public async Task BootstrapRequiresConfirmedActiveMfaAccountBeforeRoleGrant()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await database.Users.Where(item => item.Id == account.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, false));
        }
        (await PromoteAsync(account)).Should().BeFalse();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await database.Users.Where(item => item.Id == account.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, true).SetProperty(item => item.TwoFactorEnabled, false));
        }
        (await PromoteAsync(account)).Should().BeFalse();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            await database.Users.Where(item => item.Id == account.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.TwoFactorEnabled, true).SetProperty(item => item.Status, "Blocked"));
        }
        (await PromoteAsync(account)).Should().BeFalse();
    }

    [Fact]
    public async Task WrongProofCannotGrantRolesAndTriggersLockout()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        for (int attempt = 0; attempt < 5; attempt++) (await PromoteAsync(account, "Wrong password 42!")).Should().BeFalse();
        (await PromoteAsync(account)).Should().BeFalse();
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(account.Id.ToString()))!;
        (await users.IsLockedOutAsync(user)).Should().BeTrue();
        (await users.IsInRoleAsync(user, RoleNames.SuperAdmin)).Should().BeFalse();
    }

    [Fact]
    public async Task SuccessfulBootstrapIsAuditedRevokesSessionsAndCannotRunTwice()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string access = await account.LoginAsync(browser);
        (await PromoteAsync(account)).Should().BeTrue();
        (await PromoteAsync(account)).Should().BeFalse();
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: access)).StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(account.Id.ToString()))!;
        (await users.IsInRoleAsync(user, RoleNames.SuperAdmin)).Should().BeTrue();
        user.TwoFactorEnabled.Should().BeTrue();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var audit = await database.AuditLogs.SingleAsync();
        audit.ActorId.Should().Be(account.Id);
        audit.Action.Should().Be("SuperAdminBootstrapped");
        audit.EntityId.Should().Be(account.Id.ToString());
        audit.TraceId.Should().NotBeEmpty();
        (await database.EmailOutboxMessages.CountAsync(item => item.UserId == account.Id && item.Purpose == "SecurityNotification")).Should().Be(1);
        // Sign-in still needs MFA; a remaining recovery code works after provisioning.
        _ = await account.LoginAsync(browser, 1);
    }

    [Fact]
    public async Task ConcurrentInitialProvisioningCreatesExactlyOneAdministrator()
    {
        var first = await SecurityTestAccount.CreateAsync(fixture);
        var second = await SecurityTestAccount.CreateAsync(fixture);
        var results = await Task.WhenAll(PromoteAsync(first), PromoteAsync(second));
        results.Count(result => result).Should().Be(1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid roleId = await database.Roles.Where(item => item.NormalizedName == "SUPERADMIN").Select(item => item.Id).SingleAsync();
        (await database.UserRoles.CountAsync(item => item.RoleId == roleId)).Should().Be(1);
        (await database.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PreviouslyUsedAuthenticatorCodeCannotBootstrap()
    {
        var account = await SecurityTestAccount.CreateAsync(fixture);
        string code = TestAuthenticator.Code(account.Key);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(account.Id.ToString()))!;
            (await scope.ServiceProvider.GetRequiredService<MfaCodeVerifier>().VerifyAsync(user, code)).Should().BeTrue();
        }
        (await PromoteAsync(account, code: code)).Should().BeFalse();
    }
}
