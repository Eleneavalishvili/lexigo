using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed record HeartStatus(int Count, int Maximum, DateTimeOffset? RefillAt, DateTimeOffset ServerNow, bool Unlimited = false);

public sealed record HeartBalance(LearnerHearts Wallet, bool Unlimited)
{
    public int Count => Unlimited ? HeartRecovery.MaximumHearts : Wallet.Count;
}

public sealed class HeartBalanceService(LexiGoDbContext database, TimeProvider clock, SubscriptionQueries subscriptions)
{
    public async Task<HeartStatus> StatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var balance = await database.LearnerHearts.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? new LearnerHearts { UserId = userId };
        var now = clock.GetUtcNow();
        HeartRecovery.Refresh(balance, now);
        bool unlimited = await subscriptions.HasAsync(userId, "UnlimitedHearts", cancellationToken);
        return new(unlimited ? HeartRecovery.MaximumHearts : balance.Count, HeartRecovery.MaximumHearts, unlimited ? null : balance.RefillAt, now, unlimited);
    }

    // The caller owns the transaction and must hold the account row lock first.
    public async Task<HeartBalance> LockedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var balance = await database.LearnerHearts.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (balance is null)
        {
            balance = new LearnerHearts { UserId = userId };
            database.LearnerHearts.Add(balance);
        }
        HeartRecovery.Refresh(balance, clock.GetUtcNow());
        return new(balance, await subscriptions.HasAsync(userId, "UnlimitedHearts", cancellationToken));
    }

    public bool Spend(HeartBalance balance)
    {
        if (balance.Unlimited) return false;
        HeartRecovery.Spend(balance.Wallet, clock.GetUtcNow());
        return true;
    }

    public void Refill(HeartBalance balance)
    {
        balance.Wallet.Count = HeartRecovery.MaximumHearts;
        balance.Wallet.RefillAt = null;
    }
}
