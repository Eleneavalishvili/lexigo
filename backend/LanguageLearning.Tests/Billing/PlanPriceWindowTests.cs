using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Billing;

public sealed class PlanPriceWindowTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private static readonly DateTimeOffset Boundary = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private async Task<Guid> CreatePlanAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var plan = new SubscriptionPlan { Code = "RANGE_" + Guid.NewGuid().ToString("N")[..8], Name = "Window integrity fixture", IsActive = false, CreatedAt = DateTimeOffset.UtcNow };
        database.SubscriptionPlans.Add(plan);
        await database.SaveChangesAsync();
        return plan.Id;
    }

    private static PlanPrice Price(Guid planId, DateTimeOffset from, DateTimeOffset? to = null, string currency = "GEL", string period = "Monthly") =>
        new() { PlanId = planId, Period = period, Currency = currency, Amount = 10m, ValidFrom = from, ValidTo = to };

    [Fact]
    public async Task AdjacentWindowsAndIndependentCurrenciesPeriodsAndPlansRemainValid()
    {
        Guid plan = await CreatePlanAsync();
        Guid other = await CreatePlanAsync();
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        database.PlanPrices.AddRange(Price(plan, Boundary.AddMonths(-1), Boundary), Price(plan, Boundary),
            Price(plan, Boundary, currency: "USD"), Price(plan, Boundary, period: "Annual"), Price(other, Boundary));
        await database.SaveChangesAsync();
        Assert.Equal(4, await database.PlanPrices.CountAsync(item => item.PlanId == plan));
        database.PlanPrices.Add(Price(plan, Boundary.AddDays(-1), Boundary.AddDays(1)));
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
    }

    [Fact]
    public async Task ChangingAnEndCannotOverlapAnotherVersionAndMoneyRemainsImmutable()
    {
        Guid plan = await CreatePlanAsync();
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var first = Price(plan, Boundary.AddMonths(-1), Boundary);
        database.PlanPrices.AddRange(first, Price(plan, Boundary));
        await database.SaveChangesAsync();
        var overlap = await Assert.ThrowsAsync<PostgresException>(() => database.PlanPrices.Where(item => item.Id == first.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ValidTo, Boundary.AddDays(1))));
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, overlap.SqlState);
        var money = await Assert.ThrowsAsync<PostgresException>(() => database.PlanPrices.Where(item => item.Id == first.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Amount, 99m)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, money.SqlState);
        Assert.Equal(Boundary, await database.PlanPrices.Where(item => item.Id == first.Id).Select(item => item.ValidTo).SingleAsync());
    }

    [Fact]
    public async Task ConcurrentOverlappingVersionsAllowExactlyOneCommit()
    {
        Guid plan = await CreatePlanAsync();
        async Task<bool> InsertAsync(DateTimeOffset from)
        {
            using var scope = fixture.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            database.PlanPrices.Add(Price(plan, from));
            try { await database.SaveChangesAsync(); return true; }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
            { return false; }
        }
        var results = await Task.WhenAll(InsertAsync(Boundary), InsertAsync(Boundary.AddDays(1)));
        Assert.Single(results, item => item);
        using var scope = fixture.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().PlanPrices.CountAsync(item => item.PlanId == plan));
    }
}
