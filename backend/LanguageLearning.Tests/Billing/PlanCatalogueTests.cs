using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using LanguageLearning.Tests.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Billing;

public sealed class PlanCatalogueTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private sealed class CatalogueClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    [Fact]
    public async Task AuthorizedStaffCanCreateAndRenamePrivatePlansWithRetryAndStaleEditProtection()
    {
        var actor = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(actor.Id.ToString()))!;
        Assert.True((await users.AddToRoleAsync(user, RoleNames.Admin)).Succeeded);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        const string route = "/api/v1/admin/plans";
        var request = new
        {
            code = "DRAFT_TEST", name = "Draft test plan", reason = "Catalogue test",
            currentPassword = SecurityTestAccount.Password, idempotencyKey = Guid.NewGuid()
        };
        var token = await actor.LoginAsync(browser);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.SendAsync(HttpMethod.Post, route, request, token)).StatusCode);
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await database.Entry(user).ReloadAsync();
        Assert.True((await users.AddClaimAsync(user, new Claim(BillingPermissions.ClaimType, BillingPermissions.Manage))).Succeeded);
        token = await actor.LoginAsync(browser, recoveryIndex: 1);
        using var created = await browser.SendAsync(HttpMethod.Post, route, request, token);
        created.EnsureSuccessStatusCode();
        var plan = (await created.Content.ReadFromJsonAsync<BillingPlanSummary>())!;
        Assert.False(plan.IsActive);
        using var replay = await browser.SendAsync(HttpMethod.Post, route, request, token);
        Assert.Equal(plan, await replay.Content.ReadFromJsonAsync<BillingPlanSummary>());
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, route,
            request with { idempotencyKey = Guid.NewGuid() }, token)).StatusCode);
        Assert.DoesNotContain(plan.Code, await fixture.Client.GetStringAsync("/api/v1/plans"));

        var rename = new
        {
            name = "Renamed test plan", expectedName = plan.Name, request.reason,
            request.currentPassword, idempotencyKey = Guid.NewGuid()
        };
        using var renamed = await browser.SendAsync(HttpMethod.Put, $"{route}/{plan.Id}", rename, token);
        renamed.EnsureSuccessStatusCode();
        var updated = (await renamed.Content.ReadFromJsonAsync<BillingPlanSummary>())!;
        Assert.Equal(plan with { Name = rename.name }, updated);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Put, $"{route}/{plan.Id}",
            rename with { idempotencyKey = Guid.NewGuid() }, token)).StatusCode);
        Assert.Equal(2, await database.AuditLogs.CountAsync(row =>
            row.EntityType == "SubscriptionPlan" && row.EntityId == plan.Id.ToString()));

        var optionsResponse = await browser.SendAsync(HttpMethod.Get, $"{route}/features", bearer: token);
        optionsResponse.EnsureSuccessStatusCode();
        var options = (await optionsResponse.Content.ReadFromJsonAsync<ManagedPlanFeature[]>())!;
        var feature = Assert.Single(options, item => item.Code == "CoreListening");
        var premiumBefore = await database.PlanFeatures.Where(row => row.PlanId == BillingCatalogSeed.Premium)
            .OrderBy(row => row.FeatureId).Select(row => row.FeatureId).ToArrayAsync();
        var selection = new[] { new PlanFeatureEdit(feature.FeatureId, 3) };
        var change = new
        {
            name = updated.Name, expectedName = updated.Name, request.reason, request.currentPassword,
            features = selection, expectedFeatures = Array.Empty<PlanFeatureEdit>(), idempotencyKey = Guid.NewGuid()
        };
        var saved = await browser.SendAsync(HttpMethod.Put, $"{route}/{plan.Id}", change, token);
        saved.EnsureSuccessStatusCode();
        (await browser.SendAsync(HttpMethod.Put, $"{route}/{plan.Id}", change, token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Put, $"{route}/{plan.Id}",
            change with { idempotencyKey = Guid.NewGuid() }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Put,
            $"{route}/{BillingCatalogSeed.Premium}", change with
            {
                name = "Premium", expectedName = "Premium", idempotencyKey = Guid.NewGuid()
            }, token)).StatusCode);
        Assert.Equal(3, (await database.PlanFeatures.SingleAsync(row => row.PlanId == plan.Id)).LimitValue);
        Assert.Equal(premiumBefore, await database.PlanFeatures.Where(row => row.PlanId == BillingCatalogSeed.Premium)
            .OrderBy(row => row.FeatureId).Select(row => row.FeatureId).ToArrayAsync());
        Assert.Single(await database.AuditLogs.Where(row => row.EntityId == plan.Id.ToString() &&
            row.Action == "Billing.Plan.FeaturesUpdated").ToArrayAsync());

        var activation = new
        {
            name = updated.Name, expectedName = updated.Name, request.reason, request.currentPassword,
            isActive = true, expectedIsActive = false, idempotencyKey = Guid.NewGuid()
        };
        using var rejected = await browser.SendAsync(HttpMethod.Put, $"{route}/{plan.Id}", activation, token);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.False(await database.SubscriptionPlans.Where(row => row.Id == plan.Id)
            .Select(row => row.IsActive).SingleAsync());
        Assert.DoesNotContain(plan.Code, await fixture.Client.GetStringAsync("/api/v1/plans"));
    }

    [Fact]
    public async Task AdminWithoutBillingDelegationCannotScheduleAPrice()
    {
        var actor = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(actor.Id.ToString()))!;
        Assert.True((await users.AddToRoleAsync(user, RoleNames.Admin)).Succeeded);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var token = await actor.LoginAsync(browser);
        using var response = await browser.SendAsync(HttpMethod.Post,
            $"/api/v1/admin/plans/prices/{BillingCatalogSeed.Annual}/schedule",
            new
            {
                amount = 89.99m, startsAt = DateTimeOffset.UtcNow.AddDays(2), reason = "Test access",
                currentPassword = SecurityTestAccount.Password, idempotencyKey = Guid.NewGuid()
            }, token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Null(await database.PlanPrices.Where(item => item.Id == BillingCatalogSeed.Annual)
            .Select(item => item.ValidTo).SingleAsync());
    }

    [Fact]
    public async Task SchedulingUsesFreshProofAndAtomicallyAppendsOneFutureVersion()
    {
        var actor = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(actor.Id.ToString()))!;
        Assert.True((await users.AddToRoleAsync(user, RoleNames.SuperAdmin)).Succeeded);
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var token = await actor.LoginAsync(browser);
        var route = $"/api/v1/admin/plans/prices/{BillingCatalogSeed.Monthly}/schedule";
        var request = new
        {
            amount = 12.50m, startsAt = DateTimeOffset.UtcNow.Date.AddDays(3).ToString("yyyy-MM-dd'T'00:00:00'Z'"),
            reason = "Test catalogue change", currentPassword = SecurityTestAccount.Password, idempotencyKey = Guid.NewGuid()
        };
        Assert.Equal(HttpStatusCode.Conflict, (await browser.SendAsync(HttpMethod.Post, route,
            request with { currentPassword = "Incorrect test password" }, token)).StatusCode);
        var results = await Task.WhenAll(
            browser.SendAsync(HttpMethod.Post, route, request, token),
            browser.SendAsync(HttpMethod.Post, route, request with { idempotencyKey = Guid.NewGuid() }, token));
        var success = Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        var price = (await success.Content.ReadFromJsonAsync<ManagedPlanPrice>())!;
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var operations = await database.BillingOperations.Where(item => item.Operation == "Billing.Price.Schedule").ToArrayAsync();
        var operation = Assert.Single(operations);
        var replay = await browser.SendAsync(HttpMethod.Post, route, request with { idempotencyKey = operation.IdempotencyKey }, token);
        Assert.Equal(price, await replay.Content.ReadFromJsonAsync<ManagedPlanPrice>());
        Assert.DoesNotContain(SecurityTestAccount.Password, operation.RequestSnapshot);
        var previous = await database.PlanPrices.SingleAsync(item => item.Id == BillingCatalogSeed.Monthly);
        Assert.Equal(9.99m, previous.Amount);
        Assert.Equal(price.ValidFrom, previous.ValidTo);
        Assert.Equal(12.50m, price.Amount);
        Assert.Single(await database.AuditLogs.Where(item => item.Action == "Billing.Price.Scheduled").ToArrayAsync());
        Assert.DoesNotContain(price.Id.ToString(), await fixture.Client.GetStringAsync("/api/v1/plans"));
        using var future = fixture.CreateBillingClient(new CatalogueClock(price.ValidFrom));
        var futurePlans = await future.GetStringAsync("/api/v1/plans");
        Assert.Contains(price.Id.ToString(), futurePlans);
        Assert.DoesNotContain(BillingCatalogSeed.Monthly.ToString(), futurePlans);
    }

    [Theory]
    [InlineData(RoleNames.SuperAdmin)]
    [InlineData(RoleNames.Admin)]
    public async Task FinancialStaffCanReadRetiredPlansAndAllPricePeriodsWithoutPublishingThem(string role)
    {
        var actor = await SecurityTestAccount.CreateAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(actor.Id.ToString()))!;
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        if (role == RoleNames.Admin)
            Assert.True((await users.AddClaimAsync(user, new Claim(BillingPermissions.ClaimType, BillingPermissions.Manage))).Succeeded);
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var now = DateTimeOffset.UtcNow;
        var plan = new SubscriptionPlan { Code = "RETIRED_" + Guid.NewGuid().ToString("N")[..8], Name = "Archived test plan", IsActive = false, CreatedAt = now };
        database.SubscriptionPlans.Add(plan);
        database.PlanFeatures.Add(new PlanFeature { PlanId = plan.Id, FeatureId = 11, LimitValue = 3 });
        database.PlanPrices.AddRange(
            new PlanPrice { PlanId = plan.Id, Period = "Monthly", Amount = 12m, Currency = "GEL", ValidFrom = now.AddMonths(-2), ValidTo = now.AddDays(-1) },
            new PlanPrice { PlanId = plan.Id, Period = "Monthly", Amount = 15m, Currency = "GEL", ValidFrom = now.AddDays(7) });
        await database.SaveChangesAsync();
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await actor.LoginAsync(browser);
        var response = await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/plans?active=false", bearer: token);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var page = (await response.Content.ReadFromJsonAsync<AdministrationPage<ManagedPlan>>())!;
        Assert.All(page.Items, item => Assert.False(item.IsActive));
        var found = Assert.Single(page.Items, item => item.Id == plan.Id);
        Assert.Equal(new[] { 15m, 12m }, found.Prices.Select(item => item.Amount));
        var feature = Assert.Single(found.Features);
        Assert.Equal("CharacterConversations", feature.Code);
        Assert.Equal(3, feature.LimitValue);
        Assert.False(feature.Available);
        Assert.DoesNotContain(actor.Email, await response.Content.ReadAsStringAsync());
        var publicResponse = await fixture.Client.GetAsync("/api/v1/plans");
        Assert.DoesNotContain(plan.Code, await publicResponse.Content.ReadAsStringAsync());
        var empty = await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/plans?page=100000", bearer: token);
        Assert.Empty((await empty.Content.ReadFromJsonAsync<AdministrationPage<ManagedPlan>>())!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/plans?page=0", bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/plans?active=unknown", bearer: token)).StatusCode);
        Assert.False((await database.SubscriptionPlans.AsNoTracking().SingleAsync(item => item.Id == plan.Id)).IsActive);
    }
}
