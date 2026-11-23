using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Speech;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class LessonPreviewGrading(ContentQueries queries, LexiGoDbContext database, ISpeechRecognizer recognizer)
{
    public async Task<PreviewSpeechFeedback?> CheckSpeechAsync(Guid actor, Guid family, Guid lessonId,
        Guid versionId, Guid exerciseId, PreparedAudio audio, CancellationToken cancellationToken)
    {
        var lesson = await queries.LessonAsync(actor, family, lessonId, versionId, cancellationToken);
        var exercise = lesson?.Exercises.SingleOrDefault(item => item.Id == exerciseId);
        if (lesson is null || lesson.PrivateTextErasedAt is not null || exercise?.Type != "SpeakingCheck")
            return null;

        var language = await database.Languages.Where(item => item.Id == lesson.LanguageId)
            .Select(item => item.Code).SingleAsync(cancellationToken);
        var recognition = await recognizer.RecognizeAsync(language, audio, cancellationToken);
        if (recognition.Status is SpeechRecognitionStatus.Disabled or SpeechRecognitionStatus.Unavailable or SpeechRecognitionStatus.Busy)
            return new("Unavailable", "Local voice checking is unavailable or busy.");
        if (recognition.Status == SpeechRecognitionStatus.NoSpeech || string.IsNullOrWhiteSpace(recognition.Text))
            return new("NoSpeech", "No clear speech was detected. Try recording again.");
        if (recognition.Status == SpeechRecognitionStatus.Recognized && exercise.Answers.Any(
            answer => SpokenWordEvaluator.Matches(recognition.Text, answer.AnswerText)))
            return new("Matched", "The recognized words match the prompt.");
        return new("Mismatch", "The recognized words did not match. Try again.");
    }

    public async Task<PreviewFeedback?> CheckAsync(Guid actor, Guid family, Guid lessonId,
        Guid versionId, Guid exerciseId, string? answer, Guid[] tokenOrder,
        MatchedPair[] matches, CancellationToken cancellationToken)
    {
        var lesson = await queries.LessonAsync(actor, family, lessonId, versionId, cancellationToken);
        var exercise = lesson?.Exercises.SingleOrDefault(item => item.Id == exerciseId);
        if (lesson is null || exercise is null || lesson.PrivateTextErasedAt is not null) return null;

        bool correct;
        if (ExerciseFormats.IsChoice(exercise.Type, exercise.Options.Count))
        {
            if (!exercise.Options.Any(option => option.Text == answer)) return null;
            correct = exercise.Answers.Any(item => item.AnswerText == answer);
        }
        else if (exercise.Type == "SentenceBuilder")
        {
            var tokens = exercise.Options.ToDictionary(item => item.Id, item => item.Text);
            if (!SentenceAnswerKeys.IsSelection(tokenOrder, tokens.Keys.ToArray())) return null;
            var keys = exercise.Answers.Select(item => SentenceAnswerKeys.Read(item.OrderedTokenIds))
                .OfType<Guid[]>().ToArray();
            correct = SentenceAnswerKeys.Matches(tokenOrder, tokens, keys);
        }
        else if (exercise.Type == "Matching")
        {
            var keys = MatchingAnswerKeys.Read(exercise.Options.Select(item => new ExerciseOption
            {
                Id = item.Id, Text = item.Text, OrderNumber = item.OrderNumber,
                MatchKey = item.MatchKey, AudioMediaId = item.AudioMediaId
            }).ToArray());
            if (keys is null || matches.Length != keys.Count || matches.Any(pair => pair is null) ||
                !keys.Keys.ToHashSet().SetEquals(matches.Select(pair => pair.LeftOptionId)) ||
                !keys.Values.ToHashSet().SetEquals(matches.Select(pair => pair.RightOptionId))) return null;
            correct = matches.All(pair => keys[pair.LeftOptionId] == pair.RightOptionId);
        }
        else if (exercise.Type is "Translation" or "FillBlank" or "Listening")
        {
            if (string.IsNullOrWhiteSpace(answer)) return null;
            var rules = TextEvaluationRulesCodec.Read(exercise.EvaluationRules);
            if (rules is null) return null;
            var acceptsUnaccentedVowels = await database.Languages.AnyAsync(
                language => language.Id == lesson.LanguageId && language.Code != "ru", cancellationToken);
            correct = exercise.Answers.Any(item => TextAnswerEvaluator.Matches(answer, item.AnswerText,
                rules.IgnoreCase, rules.IgnoreTerminalPunctuation, acceptsUnaccentedVowels));
        }
        else return null;

        return new(correct, exercise.Explanation);
    }
}

public sealed record PreviewFeedback(bool Correct, string Explanation);
public sealed record PreviewSpeechFeedback(string Status, string Message);
