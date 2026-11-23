using System.Text.Json;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class ChallengeManagement(LexiGoDbContext database, RewardCatalogAccess access, TimeProvider clock)
{
    public async Task<CatalogWriteResult> SaveAsync(Guid actorId, Guid familyId, Guid? id, int revision,
        ChallengeInput input, string traceId, CancellationToken cancellationToken)
    {
        if (!Valid(input)) return new(Error: "Check challenge fields. Use a future window, bounded target/reward and a reason of 10–500 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.AllowedAsync(actorId, familyId, true, cancellationToken)) return new(Forbidden: true);
        Challenge item;
        if (id is { } existingId)
        {
            item = (await database.Challenges.FromSqlInterpolated(
                $"SELECT * FROM \"Challenges\" WHERE \"Id\" = {existingId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken))!;
            if (item is null) return new(Missing: true);
            if (item.IsArchived || item.Revision != revision) return new(Error: "This challenge changed or is archived. Refresh the catalog.");
            if (item.StartsAt <= clock.GetUtcNow()
                || await database.UserChallenges.AnyAsync(state => state.ChallengeId == existingId, cancellationToken))
                return new(Error: "Started challenges cannot be edited. Create a future challenge instead.");
            item.Revision++;
        }
        else
        {
            item = new Challenge { Title = input.Title, RuleType = input.RuleType };
            database.Challenges.Add(item);
        }
        if (input.StartsAt < clock.GetUtcNow().AddMinutes(5)) return new(Error: "Schedule at least five minutes ahead.");
        item.Title = input.Title.Trim();
        item.RuleType = input.RuleType;
        item.Target = input.Target;
        item.RewardXp = input.RewardXp;
        item.StartsAt = input.StartsAt.ToUniversalTime();
        item.EndsAt = input.EndsAt.ToUniversalTime();
        access.Audit(actorId, "Challenge", item.Id, id is null ? "ChallengeCreated" : "ChallengeUpdated", input.Reason,
            JsonSerializer.Serialize(new { item.Title, item.RuleType, item.Target, item.RewardXp, item.StartsAt, item.EndsAt, item.Revision }), traceId);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (RewardCatalogAccess.Duplicate(exception))
        { return new(Error: "An active challenge with that rule and exact time window already exists."); }
        await transaction.CommitAsync(cancellationToken);
        return new(item.Id);
    }

    public async Task<CatalogWriteResult> ArchiveAsync(Guid actorId, Guid familyId, Guid id, int revision,
        string reason, string traceId, CancellationToken cancellationToken)
    {
        if (!RewardCatalogAccess.Text(reason, 10, 500)) return new(Error: "Provide a reason of 10–500 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.AllowedAsync(actorId, familyId, true, cancellationToken)) return new(Forbidden: true);
        var item = await database.Challenges.FromSqlInterpolated(
            $"SELECT * FROM \"Challenges\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (item is null) return new(Missing: true);
        if (item.IsArchived) return new(id);
        if (item.Revision != revision) return new(Error: "This challenge changed. Refresh the catalog.");
        if (item.StartsAt <= clock.GetUtcNow()
            || await database.UserChallenges.AnyAsync(state => state.ChallengeId == id, cancellationToken))
            return new(Error: "Started challenges cannot be archived. Learner progress and rewards must stay available.");
        item.IsArchived = true;
        item.Revision++;
        access.Audit(actorId, "Challenge", id, "ChallengeArchived", reason, "Future challenge cancelled. Revision " + item.Revision, traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(id);
    }

    private static bool Valid(ChallengeInput input) =>
        RewardCatalogAccess.Text(input.Title, 1, 120) && RewardCatalogAccess.Text(input.Reason, 10, 500)
        && input.RuleType is "CompletedLessons" or "StudyDays" && input.Target is >= 1 and <= 10000
        && input.RewardXp is >= 1 and <= 1000 && input.EndsAt > input.StartsAt
        && input.EndsAt - input.StartsAt <= TimeSpan.FromDays(366);
}
