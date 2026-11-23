using System.Text.Json;
using System.Text.RegularExpressions;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class AchievementManagement(LexiGoDbContext database, RewardCatalogAccess access)
{
    public async Task<CatalogWriteResult> SaveAsync(Guid actorId, Guid familyId, Guid? id, int revision,
        AchievementInput input, string traceId, CancellationToken cancellationToken)
    {
        if (!Valid(input)) return new(Error: "Check the badge fields and provide a reason of 10–500 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.AllowedAsync(actorId, familyId, true, cancellationToken)) return new(Forbidden: true);
        Achievement item;
        if (id is { } existingId)
        {
            item = (await database.Achievements.FromSqlInterpolated(
                $"SELECT * FROM \"Achievements\" WHERE \"Id\" = {existingId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken))!;
            if (item is null) return new(Missing: true);
            if (item.IsArchived || item.Revision != revision) return new(Error: "This badge changed or is archived. Refresh the catalog.");
            if (item.Code != input.Code) return new(Error: "Badge codes are permanent. Create a new badge for a new code.");
            if (await database.UserAchievements.AnyAsync(award => award.AchievementId == existingId, cancellationToken))
                return new(Error: "An earned badge cannot be edited. Archive it and create a new badge.");
            item.Revision++;
        }
        else
        {
            item = new Achievement { Id = Guid.NewGuid(), Code = input.Code, Title = input.Title, RuleType = input.RuleType };
            database.Achievements.Add(item);
        }
        item.Title = input.Title.Trim();
        item.RuleType = input.RuleType;
        item.Threshold = input.Threshold;
        access.Audit(actorId, "Achievement", item.Id, id is null ? "AchievementCreated" : "AchievementUpdated",
            input.Reason, JsonSerializer.Serialize(new { item.Code, item.Title, item.RuleType, item.Threshold, item.Revision }), traceId);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (RewardCatalogAccess.Duplicate(exception))
        { return new(Error: "That badge code already exists, including archived badges."); }
        await transaction.CommitAsync(cancellationToken);
        return new(item.Id);
    }

    public async Task<CatalogWriteResult> ArchiveAsync(Guid actorId, Guid familyId, Guid id, int revision,
        string reason, string traceId, CancellationToken cancellationToken)
    {
        if (!RewardCatalogAccess.Text(reason, 10, 500)) return new(Error: "Provide a reason of 10–500 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.AllowedAsync(actorId, familyId, true, cancellationToken)) return new(Forbidden: true);
        var item = await database.Achievements.FromSqlInterpolated(
            $"SELECT * FROM \"Achievements\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (item is null) return new(Missing: true);
        if (item.IsArchived) return new(item.Id);
        if (item.Revision != revision) return new(Error: "This badge changed. Refresh the catalog.");
        item.IsArchived = true;
        item.Revision++;
        access.Audit(actorId, "Achievement", id, "AchievementArchived", reason,
            "Retired from new awards; earned badges and history preserved. Revision " + item.Revision, traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(id);
    }

    private static bool Valid(AchievementInput input) =>
        RewardCatalogAccess.Text(input.Code, 1, 60) && Regex.IsMatch(input.Code, "^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)
        && RewardCatalogAccess.Text(input.Title, 1, 120) && RewardCatalogAccess.Text(input.Reason, 10, 500)
        && input.RuleType is "CompletedLessons" or "GoalStreak" or "SavedWords" or "PerfectLesson"
        && input.Threshold is >= 1 and <= 10000 && (input.RuleType != "PerfectLesson" || input.Threshold == 1);
}
