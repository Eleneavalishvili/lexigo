using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Vocabulary;

public sealed record WordReviewResult(Guid Id, bool IsCorrect, DateTimeOffset DueAt, int IntervalDays);

public sealed class WordReviewService(LexiGoDbContext database, LearningAccess access, TimeProvider clock)
{
    private static readonly int[] Intervals = [1, 3, 7, 14, 30];

    public async Task<VocabularyOutcome> ReviewAsync(
        Guid userId, Guid familyId, Guid id, Guid requestKey, string answer, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var target = await (
            from saved in database.SavedWords
            join word in database.VocabularyItems on saved.VocabularyItemId equals word.Id
            join course in database.Courses on word.CourseId equals course.Id
            where saved.Id == id && saved.UserId == userId && course.IsPublished
            select new { Saved = saved, Answer = word.EnglishTranslation, word.PartOfSpeech }
        ).SingleOrDefaultAsync(cancellationToken);
        if (target is null) return new(Missing: true);

        var replay = await database.WordReviews.AsNoTracking().SingleOrDefaultAsync(
            item => item.SavedWordId == id && item.IdempotencyKey == requestKey, cancellationToken);
        if (replay is not null) return new(Result(replay));
        var now = clock.GetUtcNow();
        if (target.Saved.DueAt > now) return new(Error: "This word is not due for review yet.");

        bool correct = TextAnswerEvaluator.Matches(answer, target.Answer, ignoreCase: true, ignoreTerminalPunctuation: true);
        if (!correct && string.Equals(target.PartOfSpeech, "noun", StringComparison.OrdinalIgnoreCase))
            correct = TextAnswerEvaluator.Matches(WithoutEnglishArticle(answer), WithoutEnglishArticle(target.Answer),
                ignoreCase: true, ignoreTerminalPunctuation: true);
        int streak = correct ? Math.Min(target.Saved.CorrectStreak, Intervals.Length - 1) + 1 : 0;
        int interval = correct ? Intervals[streak - 1] : 0;
        var due = correct ? now.AddDays(interval) : now.AddMinutes(10);
        var review = new WordReview
        {
            SavedWordId = id, IdempotencyKey = requestKey, IsCorrect = correct,
            ReviewedAt = now, DueAt = due, IntervalDays = interval,
        };
        target.Saved.CorrectStreak = streak;
        target.Saved.IntervalDays = interval;
        target.Saved.DueAt = due;
        database.WordReviews.Add(review);
        await database.SaveChangesAsync(cancellationToken);
        await database.Entry(review).ReloadAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Result(review));
    }

    private static string WithoutEnglishArticle(string value)
    {
        string normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        foreach (string prefix in new[] { "the ", "a ", "an " })
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return normalized[prefix.Length..];
        return normalized;
    }

    private static WordReviewResult Result(WordReview review) =>
        new(review.Id, review.IsCorrect, review.DueAt, review.IntervalDays);
}
