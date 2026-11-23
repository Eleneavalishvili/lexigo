using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class CompletionCardManagement(LexiGoDbContext database, LearningAccess access,
    CompletionCardIssuer issuer, TimeProvider clock)
{
    public async Task<CompletionCardOutcome> ClaimAsync(Guid userId, Guid familyId, Guid unitId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var issued = await issuer.IssueForUnitLockedAsync(userId, unitId, cancellationToken);
        if (issued is not { } card) return new(Error: "Complete all published lessons in this unit before creating its card.");
        await transaction.CommitAsync(cancellationToken);
        return new(card.Id, Created: card.Created);
    }

    public async Task<CompletionCardOutcome> SetSharingAsync(Guid userId, Guid familyId, Guid id, bool shared,
        bool confirmPublicName, int revision, string traceId, CancellationToken cancellationToken)
    {
        if (shared && !confirmPublicName) return new(Error: "Confirm that your display name and unit result will be public.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var card = await database.CompletionCards.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (card is null) return new(Missing: true);
        if (card.Revision != revision) return new(Error: "This card changed. Reload before changing sharing.");
        if (card.IsShared == shared) return new(id);
        card.IsShared = shared;
        card.ShareCode = shared ? CompletionCardIssuer.NewCode() : null;
        card.Revision++;
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = userId,
            EntityType = "CompletionCard",
            EntityId = id.ToString(),
            Action = shared ? "CompletionCardSharingEnabled" : "CompletionCardSharingDisabled",
            Reason = shared ? "Owner confirmed public display name and unit result." : "Owner revoked the public card link.",
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(id);
    }
}
