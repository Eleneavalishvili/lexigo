using System.Text.Json;
using System.Text.Json.Nodes;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Billing;

public sealed record SubscriptionChangeOutcome(SubscriptionView? Value = null, bool Missing = false, string? Error = null, bool RateLimited = false);

public sealed class SubscriptionManagementService(LexiGoDbContext database, BillingAccountAccess access, SubscriptionQueries queries,
    TimeProvider clock, IOptions<BillingRenewalOptions> renewalOptions)
{
    public Task<SubscriptionChangeOutcome> CancelAsync(Guid userId, Guid familyId, Guid subscriptionId, Guid key, bool cancel, CancellationToken cancellationToken) =>
        ChangeAsync(userId, familyId, subscriptionId, key, cancel ? "Subscription.Cancel" : "Subscription.Resume",
            cancel ? "CancellationRequested" : "Resumed", new { subscriptionId }, async subscription =>
            {
                subscription.CancelAtPeriodEnd = cancel;
                if (cancel && subscription.CurrentPeriodEnd <= clock.GetUtcNow())
                {
                    var preference = await database.BillingPreferences.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
                    if (preference is not null)
                    {
                        preference.AutoRenew = false;
                        preference.RenewalConsentSubscriptionId = null;
                        preference.RenewalConsentAt = null;
                    }
                }
                return null;
            }, cancellationToken, stopsRenewal: cancel);

    public Task<SubscriptionChangeOutcome> RenewalAsync(Guid userId, Guid familyId, Guid subscriptionId, Guid key,
        bool autoRenew, Guid? cardId, CancellationToken cancellationToken) =>
        ChangeAsync(userId, familyId, subscriptionId, key, "Subscription.Renewal", "RenewalPreferenceChanged",
            new { subscriptionId, autoRenew, cardId }, async subscription =>
            {
                if (autoRenew && subscription.CancelAtPeriodEnd) return "Resume the subscription before enabling renewal.";
                var preference = await database.BillingPreferences.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
                var card = cardId is { } id ? await database.TestCards.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken) : null;
                if (cardId is not null && card is null) return "Choose one of your own test cards.";
                if (autoRenew && card is null) return "Choose an active test card before enabling renewal.";
                if (card is not null && card.Status != "Active" && (autoRenew || preference?.DefaultTestCardId != card.Id))
                    return "Unblock the test card before selecting it for renewal.";
                var price = await database.PlanPrices.SingleAsync(item => item.Id == (subscription.ScheduledPlanPriceId ?? subscription.CurrentPlanPriceId), cancellationToken);
                if (autoRenew && card!.Currency != price.Currency) return "The renewal card currency must match the next plan price.";
                if (preference is null)
                {
                    preference = new BillingPreference { UserId = userId };
                    database.BillingPreferences.Add(preference);
                }
                preference.AutoRenew = autoRenew;
                preference.DefaultTestCardId = cardId;
                preference.RenewalConsentSubscriptionId = autoRenew && renewalOptions.Value.Enabled ? subscription.Id : null;
                preference.RenewalConsentAt = autoRenew && renewalOptions.Value.Enabled ? clock.GetUtcNow() : null;
                return null;
            }, cancellationToken, stopsRenewal: !autoRenew);

    public Task<SubscriptionChangeOutcome> ScheduleAsync(Guid userId, Guid familyId, Guid subscriptionId, Guid key,
        Guid? priceId, CancellationToken cancellationToken) =>
        ChangeAsync(userId, familyId, subscriptionId, key, "Subscription.Schedule", "PlanChangeScheduled",
            new { subscriptionId, priceId }, async subscription =>
            {
                if (priceId is { } id)
                {
                    var now = clock.GetUtcNow();
                    var price = await database.PlanPrices.SingleOrDefaultAsync(item => item.Id == id && item.PlanId == subscription.PlanId
                        && item.ValidFrom <= now && (item.ValidTo == null || item.ValidTo > now), cancellationToken);
                    if (price is null || !await database.SubscriptionPlans.AnyAsync(item => item.Id == price.PlanId && item.IsActive, cancellationToken))
                        return "Choose a currently available price for your plan.";
                    var preference = await database.BillingPreferences.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
                    if (preference?.AutoRenew == true)
                    {
                        var card = await database.TestCards.SingleOrDefaultAsync(item => item.Id == preference.DefaultTestCardId && item.UserId == userId, cancellationToken);
                        if (card is null || card.Status != "Active" || card.Currency != price.Currency)
                            return "Choose an active renewal card in the scheduled price currency first.";
                    }
                }
                subscription.ScheduledPlanPriceId = priceId;
                return null;
            }, cancellationToken);

    private async Task<SubscriptionChangeOutcome> ChangeAsync(Guid userId, Guid familyId, Guid subscriptionId, Guid key,
        string operation, string eventType, object payload, Func<Subscription, Task<string?>> change, CancellationToken cancellationToken, bool stopsRenewal = false)
    {
        if (key == Guid.Empty || subscriptionId == Guid.Empty) return new(Error: "Use a valid subscription and request key.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        string request = JsonSerializer.Serialize(payload);
        var previous = await database.BillingOperations.SingleOrDefaultAsync(item => item.UserId == userId && item.Operation == operation && item.IdempotencyKey == key, cancellationToken);
        if (previous is not null) return JsonNode.DeepEquals(JsonNode.Parse(request), JsonNode.Parse(previous.RequestSnapshot))
            ? new(JsonSerializer.Deserialize<SubscriptionView>(previous.ResponseSnapshot)!) : new(Error: "This request key was used for different subscription details.");
        var subscription = await database.Subscriptions.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == subscriptionId, cancellationToken);
        if (subscription is null) return new(Missing: true);
        var now = clock.GetUtcNow();
        if (subscription.Status != "Active" || subscription.CurrentPeriodStart > now || subscription.CurrentPeriodEnd <= now)
        {
            // Late opt-out is allowed while a due worker waits. It must never target a newer paid subscription.
            if (!stopsRenewal || subscription.CurrentPeriodEnd > now || await queries.CurrentAsync(userId, cancellationToken) is not null)
                return new(Error: "Only your current paid period can be changed, except to stop a pending renewal.");
            var preference = await database.BillingPreferences.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (preference?.RenewalConsentSubscriptionId is { } target && target != subscription.Id)
                return new(Error: "This renewal preference belongs to a newer subscription.");
        }
        // Positive edits are capped; the business quota must never block cancellation or renewal OFF.
        if (!stopsRenewal && await database.BillingOperations.CountAsync(item => item.UserId == userId && item.Operation.StartsWith("Subscription.") && item.CreatedAt > now.AddMinutes(-10), cancellationToken) >= 20)
            return new(RateLimited: true);
        var error = await change(subscription);
        if (error is not null) return new(Error: error);
        database.ChangeTracker.DetectChanges();
        bool changed = database.Entry(subscription).State == EntityState.Modified || database.ChangeTracker.Entries<BillingPreference>().Any(item => item.State is EntityState.Added or EntityState.Modified);
        if (changed) database.SubscriptionEvents.Add(new SubscriptionEvent { SubscriptionId = subscription.Id, EventType = eventType, ActorId = userId, OccurredAt = now });
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = userId,
            Action = operation,
            EntityType = "Subscription",
            EntityId = subscription.Id.ToString(),
            Reason = "Saved simulated subscription preferences; no funds charged.",
            TraceId = key.ToString(),
            OccurredAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        var response = await queries.SubscriptionAsync(userId, cancellationToken);
        database.BillingOperations.Add(new BillingOperation
        {
            UserId = userId,
            Operation = operation,
            IdempotencyKey = key,
            RequestSnapshot = request,
            ResponseSnapshot = JsonSerializer.Serialize(response),
            CreatedAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(response);
    }
}
