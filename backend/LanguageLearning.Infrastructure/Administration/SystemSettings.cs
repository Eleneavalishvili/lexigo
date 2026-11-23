using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed record StudyDefaults(int LessonXp, int DailyGoalXp, int AudioUploadMiB, int StudyDays,
    [property: JsonRequired] TimeOnly ReminderTime, int DialogueXp = 10)
{
    public static StudyDefaults Standard { get; } = new(20, 20, 10, 31, new(18, 0));
}

public sealed record StudySettingsView(StudyDefaults Defaults, DateTimeOffset? UpdatedAt);

public sealed record UpdateStudySettings([Required] StudyDefaults Defaults, DateTimeOffset? ExpectedUpdatedAt,
    [Required, StringLength(500, MinimumLength = 3)] string Reason);

public sealed class SystemSettings(LexiGoDbContext database, ContentAccess access, TimeProvider clock)
{
    private const string StudyKey = "study-defaults";

    public async Task<StudySettingsView> ReadAsync(CancellationToken token)
    {
        var setting = await database.SystemSettings.AsNoTracking().SingleOrDefaultAsync(item => item.Key == StudyKey, token);
        return new(Parse(setting), setting?.UpdatedAt);
    }

    public async Task<StudySettingsView?> AdminReadAsync(Guid actor, Guid family, CancellationToken token) =>
        await IsSuperAdminAsync(actor, family, false, token) ? await ReadAsync(token) : null;

    public async Task<CatalogWriteResult> UpdateAsync(Guid actor, Guid family, UpdateStudySettings input,
        string traceId, CancellationToken token)
    {
        if (input.Defaults is not { LessonXp: >= 1 and <= 1000, DailyGoalXp: >= 1 and <= 1000,
                AudioUploadMiB: >= 1 and <= 10, StudyDays: >= 1 and <= 127 } defaults
            || defaults.ReminderTime.Ticks % TimeSpan.TicksPerMinute != 0
            || defaults.DialogueXp is < 0 or > 1000
            || !RewardCatalogAccess.Text(input.Reason, 3, 500))
            return new(Error: "Enter valid study defaults and a reason.");

        await using var transaction = await database.Database.BeginTransactionAsync(token);
        if (!await IsSuperAdminAsync(actor, family, true, token)) return new(Forbidden: true);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730027002)", token);
        var setting = await database.SystemSettings.SingleOrDefaultAsync(item => item.Key == StudyKey, token);
        var previous = Parse(setting);
        if (previous == defaults) return new();
        if (setting?.UpdatedAt != input.ExpectedUpdatedAt)
            return new(Error: "Settings changed. Reload before saving again.");

        var now = clock.GetUtcNow();
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
        if (setting is not null && now <= setting.UpdatedAt)
            now = setting.UpdatedAt.AddTicks(TimeSpan.TicksPerMicrosecond);
        if (setting is null)
        {
            setting = new SystemSetting { Key = StudyKey, Value = "" };
            database.SystemSettings.Add(setting);
        }

        setting.Value = JsonSerializer.Serialize(defaults);
        setting.UpdatedBy = actor;
        setting.UpdatedAt = now;
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actor, EntityType = "SystemSetting", EntityId = StudyKey, Action = "SystemSettingsChanged",
            Reason = input.Reason.Trim() + " | " + JsonSerializer.Serialize(previous) + " → " + setting.Value,
            TraceId = traceId, OccurredAt = now
        });
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new();
    }

    private async Task<bool> IsSuperAdminAsync(Guid actor, Guid family, bool forUpdate, CancellationToken token) =>
        (await access.ActorAsync(actor, family, forUpdate, token))?.Roles.Contains(RoleNames.SuperAdmin) == true;

    private static StudyDefaults Parse(SystemSetting? setting) =>
        setting is null ? StudyDefaults.Standard : JsonSerializer.Deserialize<StudyDefaults>(setting.Value)!;
}
