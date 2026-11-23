using System.Text.Json;
using System.Text.Json.Nodes;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed record BillingPlanEdit(Guid? Id, string? Code, string Name, string? ExpectedName,
    string Reason, string CurrentPassword, Guid Key, PlanFeatureEdit[]? Features = null,
    PlanFeatureEdit[]? ExpectedFeatures = null, bool? IsActive = null, bool? ExpectedIsActive = null);
public sealed record PlanFeatureEdit(int FeatureId, int? LimitValue);
public sealed record BillingPlanSummary(Guid Id, string Code, string Name, bool IsActive);
public sealed record BillingPlanEditOutcome(BillingPlanSummary? Plan = null, bool Forbidden = false,
    bool Missing = false, string? Error = null);

public sealed class BillingPlanEditor(LexiGoDbContext database, BillingAdministrationAccess access, TimeProvider clock)
{
    private const string Operation = "Billing.Plan.Edit";

    public async Task<BillingPlanEditOutcome> SaveAsync(Guid actorId, Guid familyId, BillingPlanEdit edit,
        CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken)) return new(Forbidden: true);
        string name = edit.Name.Trim();
        string reason = edit.Reason.Trim();
        string? code = edit.Code?.Trim().ToUpperInvariant();
        if (edit.Key == Guid.Empty || name.Length is < 2 or > 80 || name.Any(char.IsControl) ||
            reason.Length is < 3 or > 500 || reason.Any(char.IsControl))
            return new(Error: "Enter a valid plan name and a reason of 3–500 characters.");
        if (edit.Id is null && (code is null || code.Length is < 2 or > 24 ||
            code.Any(character => !(char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character == '_'))))
            return new(Error: "Use a plan code with letters, digits or underscores.");
        if (!PlanDraftFeatures.Valid(edit.Features, edit.ExpectedFeatures))
            return new(Error: "Use distinct catalogue features and positive limits, or leave the limit blank.");
        if (edit.IsActive.HasValue != edit.ExpectedIsActive.HasValue || (edit.IsActive.HasValue && edit.Features is not null))
            return new(Error: "Save availability and feature changes separately.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AdministrationLocks.AcquireAsync(database, [actorId], cancellationToken);
        if (!await access.AllowedAsync(actorId, familyId, cancellationToken)) return new(Forbidden: true);
        if (!await access.RecentProofAsync(actorId, familyId, edit.CurrentPassword, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(Error: "Use your current password and a two-factor sign-in from the last five minutes.");
        }

        string request = RequestSnapshot(edit, code, name, reason);
        var replay = await database.BillingOperations.SingleOrDefaultAsync(item => item.UserId == actorId &&
            item.Operation == Operation && item.IdempotencyKey == edit.Key, cancellationToken);
        if (replay is not null)
            return JsonNode.DeepEquals(JsonNode.Parse(replay.RequestSnapshot), JsonNode.Parse(request))
                ? new(JsonSerializer.Deserialize<BillingPlanSummary>(replay.ResponseSnapshot))
                : new(Error: "This request key belongs to a different plan change.");

        var now = clock.GetUtcNow();
        SubscriptionPlan plan;
        if (edit.Id is { } id)
        {
            var existing = await database.SubscriptionPlans.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (existing is null) return new(Missing: true);
            if (existing.Name != edit.ExpectedName) return new(Error: "Reload the catalogue before renaming this plan.");
            if (edit.IsActive is { } active)
            {
                if (existing.Id != BillingCatalogSeed.Premium)
                    return new(Error: "Only the existing Premium plan supports changes to sales availability.");
                if (existing.IsActive != edit.ExpectedIsActive || existing.IsActive == active)
                    return new(Error: "Reload the catalogue before changing sales availability.");
                existing.IsActive = active;
            }
            if (edit.Features is not null)
            {
                var error = await PlanDraftFeatures.ApplyAsync(database, existing, edit.Features,
                    edit.ExpectedFeatures!, cancellationToken);
                if (error is not null) return new(Error: error);
            }
            plan = existing;
            plan.Name = name;
        }
        else
        {
            if (await database.SubscriptionPlans.AnyAsync(item => item.Code == code, cancellationToken))
                return new(Error: "This plan code is already in use.");
            plan = new() { Code = code!, Name = name, IsActive = false, CreatedAt = now };
            database.SubscriptionPlans.Add(plan);
        }

        var response = new BillingPlanSummary(plan.Id, plan.Code, plan.Name, plan.IsActive);
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId, Action = AuditAction(edit),
            EntityType = "SubscriptionPlan", EntityId = plan.Id.ToString(), Reason = reason,
            TraceId = edit.Key.ToString(), OccurredAt = now
        });
        database.BillingOperations.Add(new BillingOperation
        {
            UserId = actorId, Operation = Operation, IdempotencyKey = edit.Key, CreatedAt = now,
            RequestSnapshot = request, ResponseSnapshot = JsonSerializer.Serialize(response)
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(response);
    }

    private static string RequestSnapshot(BillingPlanEdit edit, string? code, string name, string reason)
    {
        if (edit.IsActive.HasValue)
            return JsonSerializer.Serialize(new { edit.Id, code, name, edit.ExpectedName, reason, edit.IsActive, edit.ExpectedIsActive });
        if (edit.Features is not null)
            return JsonSerializer.Serialize(new
            {
                edit.Id, code, name, edit.ExpectedName, reason,
                Features = edit.Features.OrderBy(item => item.FeatureId),
                ExpectedFeatures = edit.ExpectedFeatures?.OrderBy(item => item.FeatureId)
            });
        // Earlier metadata edits have durable replay snapshots without feature or availability fields.
        return JsonSerializer.Serialize(new { edit.Id, code, name, edit.ExpectedName, reason });
    }

    private static string AuditAction(BillingPlanEdit edit) => edit switch
    {
        { Id: null } => "Billing.Plan.DraftCreated",
        { IsActive: true } => "Billing.Plan.Reactivated",
        { IsActive: false } => "Billing.Plan.Retired",
        { Features: not null } => "Billing.Plan.FeaturesUpdated",
        _ => "Billing.Plan.Renamed"
    };
}
