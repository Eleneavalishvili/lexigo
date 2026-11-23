using System.Text;
using System.Text.Json;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed record StarterVocabularyWord(string Key, string Word, string EnglishTranslation,
    string Example, string PartOfSpeech, string Topic);
public sealed record StarterVocabularyPack(string Code, StarterVocabularyWord[] Items);

public sealed class StarterVocabularyInstaller(LexiGoDbContext database, TimeProvider clock)
{
    public static StarterVocabularyPack[] Read()
    {
        using var stream = typeof(StarterVocabularyInstaller).Assembly.GetManifestResourceStream(
            "LanguageLearning.Infrastructure.Vocabulary.Starter.words.json")
            ?? throw new InvalidOperationException("Built-in vocabulary resource is missing.");
        return JsonSerializer.Deserialize<StarterVocabularyPack[]>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Built-in vocabulary resource is invalid.");
    }

    public async Task<int> InstallAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730027021)", cancellationToken);
        int installed = 0;
        foreach (var pack in Read())
        {
            Guid courseId = StarterLessonInstaller.Read(pack.Code).CourseId;
            await database.Courses.FromSqlInterpolated($"SELECT * FROM \"Courses\" WHERE \"Id\" = {courseId} FOR UPDATE")
                .SingleAsync(cancellationToken);
            var existing = await database.VocabularyItems.Where(item => item.CourseId == courseId)
                .Select(item => new { item.Id, item.Word }).ToArrayAsync(cancellationToken);
            var ids = existing.Select(item => item.Id).ToHashSet();
            var spellings = existing.Select(item => Normalize(item.Word)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var source in pack.Items)
            {
                if (string.IsNullOrWhiteSpace(source.Key) || !Valid(source.Word, 160)
                    || !Valid(source.EnglishTranslation, 300) || !Valid(source.Example, 2000)
                    || !Valid(source.PartOfSpeech, 60)
                    || source.Topic is not ("greetings" or "numbers" or "family" or "food" or "travel" or "routines"))
                    throw new InvalidOperationException("Built-in vocabulary contains an invalid entry.");
                Guid id = StarterLessonInstaller.ContentId("vocabulary/" + pack.Code + "/" + source.Key);
                // Never overwrite staff edits, recorded audio, existing saves or their review schedules.
                if (ids.Contains(id) || !spellings.Add(Normalize(source.Word))) continue;
                database.VocabularyItems.Add(new VocabularyItem
                {
                    Id = id, CourseId = courseId, Word = source.Word,
                    EnglishTranslation = source.EnglishTranslation, Example = source.Example,
                    PartOfSpeech = source.PartOfSpeech, Topic = source.Topic
                });
                ids.Add(id);
                database.AuditLogs.Add(new AuditLog
                {
                    Action = "Vocabulary.BuiltInInstalled", EntityType = "VocabularyItem",
                    EntityId = id.ToString(), TraceId = "starter-vocabulary/" + pack.Code + "/" + source.Key,
                    Reason = "Original built-in learning vocabulary. No independent human review claimed.",
                    OccurredAt = clock.GetUtcNow()
                });
                installed++;
            }
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return installed;
    }

    private static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC);
    private static bool Valid(string value, int maximum) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximum && !value.Contains('\0');
}
