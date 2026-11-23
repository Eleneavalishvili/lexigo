using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LanguageLearning.Infrastructure.Content;

public sealed record CatalogWriteResult(Guid? Id = null, string? Error = null, bool Forbidden = false, bool Missing = false);
public sealed record CatalogPage<T>(T[] Items, int Page, int PageSize, bool HasMore, int TotalCount);
public sealed record AchievementInput(string Code, string Title, string RuleType, int Threshold, string Reason);
public sealed record ChallengeInput(string Title, string RuleType, int Target, int RewardXp,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason);

public sealed class RewardCatalogAccess(LexiGoDbContext database, ContentAccess access, TimeProvider clock)
{
    public async Task<bool> AllowedAsync(Guid actorId, Guid familyId, bool forUpdate, CancellationToken cancellationToken) =>
        await access.ActorAsync(actorId, familyId, forUpdate, cancellationToken) is { IsAdmin: true };

    public void Audit(Guid actorId, string entityType, Guid id, string action, string reason, string details, string traceId)
    {
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            EntityType = entityType,
            EntityId = id.ToString(),
            Action = action,
            Reason = reason.Trim() + " | " + details,
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });
    }

    public static bool Text(string? value, int min, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= min && value.Length <= max && !value.Contains('\0');

    public static bool Duplicate(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
