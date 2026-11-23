using System.Text.Json;
using System.Text.Json.Nodes;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed record PriceScheduleOutcome(ManagedPlanPrice? Price = null, bool Forbidden = false,
    bool Missing = false, string? Error = null);

public sealed class PlanPriceScheduling(LexiGoDbContext database, BillingAdministrationAccess access, TimeProvider clock)
{
    private const string Operation = "Billing.Price.Schedule";

    public async Task<PriceScheduleOutcome> ScheduleAsync(Guid actorId, Guid familyId, Guid priceId,
        decimal amount, DateTimeOffset startsAt, string reason, string password, Guid key, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken)) return new(Forbidden: true);
        if (key == Guid.Empty || amount is <= 0 or > 10000 || decimal.Round(amount, 2) != amount ||
            reason.Trim().Length is < 3 or > 500 || reason.Any(char.IsControl))
            return new(Error: "Enter a valid simulated amount and a reason of 3–500 characters.");

        startsAt = DateTimeOffset.FromUnixTimeMilliseconds(startsAt.ToUnixTimeMilliseconds());
        reason = reason.Trim();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AdministrationLocks.AcquireAsync(database, [actorId], cancellationToken);
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken)) return new(Forbidden: true);
        if (!await access.RecentProofAsync(actorId, familyId, password, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(Error: "Use your current password and a two-factor sign-in from the last five minutes.");
        }

        var request = JsonSerializer.Serialize(new { priceId, amount = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), startsAt, reason });
        var replay = await database.BillingOperations.SingleOrDefaultAsync(item => item.UserId == actorId &&
            item.Operation == Operation && item.IdempotencyKey == key, cancellationToken);
        if (replay is not null)
            return JsonNode.DeepEquals(JsonNode.Parse(replay.RequestSnapshot), JsonNode.Parse(request))
                ? new(JsonSerializer.Deserialize<ManagedPlanPrice>(replay.ResponseSnapshot))
                : new(Error: "This request key belongs to a different price change.");

        var previous = await database.PlanPrices.SingleOrDefaultAsync(item => item.Id == priceId, cancellationToken);
        if (previous is null) return new(Missing: true);
        var now = clock.GetUtcNow();
        if (previous.ValidTo is not null || startsAt <= now || startsAt <= previous.ValidFrom)
            return new(Error: "Choose an open-ended price and a future time after it starts.");
        if (!await database.SubscriptionPlans.AnyAsync(item => item.Id == previous.PlanId && item.IsActive, cancellationToken))
            return new(Error: "Only active plans can receive a future price.");
        if (await database.PlanPrices.AnyAsync(item => item.PlanId == previous.PlanId &&
            item.Period == previous.Period && item.Currency == previous.Currency &&
            item.ValidFrom > previous.ValidFrom, cancellationToken))
            return new(Error: "Reload the catalogue before scheduling another price.");

        previous.ValidTo = startsAt;
        await database.SaveChangesAsync(cancellationToken);
        var next = new PlanPrice
        {
            PlanId = previous.PlanId, Period = previous.Period, Currency = previous.Currency,
            Amount = amount, ValidFrom = startsAt
        };
        var result = new ManagedPlanPrice(next.Id, next.Period, next.Amount, next.Currency, startsAt, null);
        database.PlanPrices.Add(next);
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId, Action = "Billing.Price.Scheduled", EntityType = "PlanPrice",
            EntityId = next.Id.ToString(), Reason = reason, TraceId = key.ToString(), OccurredAt = now
        });
        database.BillingOperations.Add(new BillingOperation
        {
            UserId = actorId, Operation = Operation, IdempotencyKey = key, CreatedAt = now,
            RequestSnapshot = request, ResponseSnapshot = JsonSerializer.Serialize(result)
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(result);
    }
}
