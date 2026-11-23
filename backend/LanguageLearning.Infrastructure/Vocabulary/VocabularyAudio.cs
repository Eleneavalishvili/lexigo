using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed class VocabularyAudio(LexiGoDbContext database, Lazy<MediaObjectStore> storage,
    IOptions<ObjectStorageOptions> options)
{
    public async Task<LearningOutcome> PlaybackAsync(Guid wordId, CancellationToken cancellationToken)
    {
        var clip = await (from word in database.VocabularyItems
                         join course in database.Courses on word.CourseId equals course.Id
                         join media in ReviewedAudio.Ready(database.MediaAssets) on word.AudioMediaId equals (Guid?)media.Id
                         where word.Id == wordId && course.IsPublished && media.LanguageId == course.TargetLanguageId
                         select new { media.StorageKey, media.Source, media.License }).SingleOrDefaultAsync(cancellationToken);
        if (clip is null) return new(Missing: true);
        if (!options.Value.Enabled) return new(Unavailable: true);
        string url = await storage.Value.CreateReadLinkAsync(clip.StorageKey, TimeSpan.FromMinutes(5));
        return new(new WordAudioLink(url, 300, clip.Source, clip.License));
    }
}
