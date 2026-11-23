using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

internal static class ExerciseSkillTracking
{
    internal static string? DefaultCategory(string type) => type switch
    {
        "MultipleChoice" or "Translation" or "Matching" => "vocabulary",
        "FillBlank" or "SentenceBuilder" => "grammar",
        "Reading" or "DialogueChoice" => "reading",
        "Listening" => "listening",
        "Alphabet" => "alphabet",
        _ => null
    };

    internal static async Task RecordAsync(LexiGoDbContext database, LearningSession session,
        string exerciseType, int[]? snapshotSkillIds, bool correct, DateTimeOffset submittedAt,
        CancellationToken cancellationToken)
    {
        string? legacyCategory = DefaultCategory(exerciseType);
        if (snapshotSkillIds is null && legacyCategory is null) return;

        var catalog = database.Skills.Where(skill => skill.CourseId == session.CourseId);
        var selected = snapshotSkillIds is null
            ? catalog.Where(skill => skill.Category == legacyCategory)
            : catalog.Where(skill => snapshotSkillIds.Contains(skill.Id));
        var ids = await selected.Select(skill => skill.Id).ToArrayAsync(cancellationToken);
        if (ids.Length == 0) return;

        var existing = await database.SkillProgress
            .Where(item => item.UserId == session.UserId && ids.Contains(item.SkillId))
            .ToDictionaryAsync(item => item.SkillId, cancellationToken);
        foreach (int id in ids)
        {
            if (!existing.TryGetValue(id, out var progress))
            {
                progress = new SkillProgress { UserId = session.UserId, SkillId = id };
                database.SkillProgress.Add(progress);
            }

            if (correct) progress.CorrectCount++;
            else progress.IncorrectCount++;
            progress.LastPracticedAt = submittedAt;
        }
    }
}
