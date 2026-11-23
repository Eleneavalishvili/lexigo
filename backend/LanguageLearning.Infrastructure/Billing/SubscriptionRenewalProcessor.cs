using System.Globalization;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class SubscriptionRenewalProcessor(LexiGoDbContext database, SubscriptionPaymentRecorder recorder,
    TimeProvider clock, IOptions<BillingRenewalOptions> options, ILogger<SubscriptionRenewalProcessor> logger)
{
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return 0;
        var now = clock.GetUtcNow();
        var due = await database.Subscriptions.AsNoTracking().Where(item => item.Status == "Active" && item.CurrentPeriodEnd <= now
            && database.Users.Any(user => user.Id == item.UserId && user.Status == "Active" && user.EmailConfirmed))
            .OrderBy(item => item.CurrentPeriodEnd).ThenBy(item => item.Id).Take(50)
            .Select(item => new { item.Id, item.UserId }).ToArrayAsync(cancellationToken);
        int completed = 0;
        foreach (var candidate in due)
        {
            // A batch scope can see multiple subscriptions for one account; never reuse tracked state across transactions.
            database.ChangeTracker.Clear();
            try
            {
                if (await ProcessAsync(candidate.UserId, candidate.Id, cancellationToken)) completed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning("A simulated renewal transaction rolled back ({ExceptionType}).", exception.GetType().Name);
            }
        }
        return completed;
    }

    private async Task<bool> ProcessAsync(Guid userId, Guid subscriptionId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Same user-first lock as cards, preferences and manual purchases. No learner session is impersonated.
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || user.Status != "Active" || !user.EmailConfirmed) return false;
        var now = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds());
        var subscription = await database.Subscriptions.SingleAsync(item => item.Id == subscriptionId && item.UserId == userId, cancellationToken);
        if (subscription.Status != "Active" || subscription.CurrentPeriodEnd > now) return false;
        var end = subscription.CurrentPeriodEnd;
        string periodKey = end.ToString("O", CultureInfo.InvariantCulture);
        if (await database.BillingJobExecutions.AnyAsync(item => item.SubscriptionId == subscription.Id && item.PeriodKey == periodKey, cancellationToken)) return false;
        var preference = await database.BillingPreferences.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        string? reason = subscription.CancelAtPeriodEnd ? "Cancelled" : preference?.AutoRenew != true ? "AutoRenewOff"
            : preference.RenewalConsentSubscriptionId != subscription.Id || preference.RenewalConsentAt is null || preference.RenewalConsentAt > end ? "ConsentRequired"
            : now > end.AddMinutes(options.Value.GraceMinutes) ? "MissedWindow" : null;
        var job = new BillingJobExecution
        {
            SubscriptionId = subscription.Id,
            PeriodKey = periodKey,
            LastAttemptAt = now,
            Status = "Skipped",
            FailureCode = reason
        };
        if (reason is null)
        {
            var previousPrice = await database.PlanPrices.SingleAsync(item => item.Id == subscription.CurrentPlanPriceId, cancellationToken);
            var prices = database.PlanPrices.Where(item => item.PlanId == subscription.PlanId && item.ValidFrom <= end
                && (item.ValidTo == null || item.ValidTo > end)
                && database.SubscriptionPlans.Any(plan => plan.Id == item.PlanId && plan.IsActive));
            var price = subscription.ScheduledPlanPriceId is { } scheduledId
                ? await prices.SingleOrDefaultAsync(item => item.Id == scheduledId, cancellationToken)
                : await prices.SingleOrDefaultAsync(item => item.Id == previousPrice.Id, cancellationToken);
            var card = preference!.DefaultTestCardId is { } cardId
                ? await database.TestCards.SingleOrDefaultAsync(item => item.Id == cardId && item.UserId == userId, cancellationToken) : null;
            if (price is null) reason = "PriceUnavailable";
            else if (card is null) reason = "MissingCard";
            else
            {
                reason = card.Status != "Active" ? "CardBlocked" : card.Currency != price.Currency ? "CurrencyMismatch"
                    : card.Balance < price.Amount ? "InsufficientBalance" : null;
                var order = new BillingOrder
                {
                    UserId = userId,
                    PlanPriceId = price.Id,
                    SubscriptionId = subscription.Id,
                    IdempotencyKey = job.Id,
                    AmountSnapshot = price.Amount,
                    Currency = price.Currency,
                    PeriodSnapshot = price.Period,
                    Status = reason is null ? "Created" : "Expired",
                    OrderType = "Renewal",
                    RenewalPeriodKey = periodKey,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(30)
                };
                var payment = new PaymentAttempt
                {
                    UserId = userId,
                    OrderId = order.Id,
                    TestCardId = card.Id,
                    IdempotencyKey = job.Id,
                    Amount = order.AmountSnapshot,
                    Currency = order.Currency,
                    Status = reason is null ? "Succeeded" : "Failed",
                    FailureCode = reason,
                    CreatedAt = now,
                    CompletedAt = now
                };
                database.BillingOrders.Add(order);
                if (reason is null)
                {
                    subscription.CurrentPeriodStart = end;
                    subscription.CurrentPeriodEnd = price.Period == "Monthly" ? end.AddMonths(1) : end.AddYears(1);
                    subscription.CurrentPlanPriceId = price.Id;
                    subscription.ScheduledPlanPriceId = null;
                }
                recorder.Record(payment, order, card, subscription, "Renewed", null);
                job.Status = reason is null ? "Succeeded" : "Failed";
            }
        }
        job.FailureCode = reason;
        if (reason is not null)
        {
            subscription.Status = "Expired";
            database.SubscriptionEvents.Add(new SubscriptionEvent { SubscriptionId = subscription.Id, EventType = "Expired", OccurredAt = now });
            if (preference is not null)
            {
                preference.AutoRenew = false;
                preference.RenewalConsentAt = null;
                preference.RenewalConsentSubscriptionId = null;
            }
        }
        database.BillingJobExecutions.Add(job);
        database.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = reason is null ? "SubscriptionRenewed" : "SubscriptionEnded",
            Title = reason is null ? "Your test Premium plan renewed" : "Your Premium period has ended",
            Body = reason is null ? "Your simulated card was charged for the next period. View your payment and test receipt in Billing. No real money was used."
                : $"{EndReason(reason)} No funds were charged. Your learning progress is saved. You can purchase a new test period manually in Billing.",
            CreatedAt = now
        });
        database.AuditLogs.Add(new AuditLog
        {
            Action = "Billing.Renewal." + job.Status,
            EntityType = "BillingJobExecution",
            EntityId = job.Id.ToString(),
            Reason = reason ?? "Simulated next period paid; no real money.",
            TraceId = job.Id.ToString(),
            OccurredAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static string EndReason(string reason) => reason switch
    {
        "Cancelled" => "Your period-end cancellation was applied.",
        "AutoRenewOff" => "Automatic renewal was off.",
        "ConsentRequired" => "Automatic renewal had not been confirmed.",
        "MissedWindow" => "Processing was too late to renew this period.",
        "PriceUnavailable" => "Your selected price is no longer available.",
        "MissingCard" => "No renewal card was selected.",
        "CardBlocked" => "Your renewal card was blocked.",
        "CurrencyMismatch" => "Your renewal card currency did not match the selected price.",
        "InsufficientBalance" => "Your renewal card had insufficient simulated funds.",
        _ => "Your test renewal could not complete."
    };
}
