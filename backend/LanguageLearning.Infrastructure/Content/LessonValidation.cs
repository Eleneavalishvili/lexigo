using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class LessonValidation(LexiGoDbContext database)
{
    public async Task<string?> ValidateAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await database.LessonVersions.SingleAsync(item => item.Id == versionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(version.Title) || string.IsNullOrWhiteSpace(version.Objective) || string.IsNullOrWhiteSpace(version.Explanation)) return "Add a title, objective and English explanation.";
        var exercises = await database.Exercises.Where(item => item.LessonVersionId == versionId).ToArrayAsync(cancellationToken);
        if (exercises.Length is < 1 or > 100) return "A lesson needs 1–100 exercises.";
        var ids = exercises.Select(item => item.Id).ToArray();
        var options = await database.ExerciseOptions.Where(item => ids.Contains(item.ExerciseId)).ToArrayAsync(cancellationToken);
        var answers = await database.AcceptedAnswers.Where(item => ids.Contains(item.ExerciseId)).ToArrayAsync(cancellationToken);
        var courseLanguage = await (from lesson in database.Lessons
                                    join unit in database.Units on lesson.UnitId equals unit.Id
                                    join course in database.Courses on unit.CourseId equals course.Id
                                    join language in database.Languages on course.TargetLanguageId equals language.Id
                                    where lesson.Id == version.LessonId
                                    select new { language.Id, language.Code }).SingleAsync(cancellationToken);
        var mediaIds = exercises.Where(item => item.AudioMediaId.HasValue).Select(item => item.AudioMediaId!.Value)
            .Concat(options.Where(item => item.AudioMediaId.HasValue).Select(item => item.AudioMediaId!.Value)).Distinct().ToArray();
        var reviewedMedia = await ReviewedAudio.ForLanguage(database.MediaAssets.AsNoTracking(), courseLanguage.Id)
            .Where(item => mediaIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var exercise in exercises)
        {
            // Other specified exercise forms remain required; block publication until their validators exist.
            if (!ExerciseFormats.SupportsLearning(exercise.Type)) return "This exercise type is not yet ready for publication.";
            bool contextual = ExerciseFormats.HasContext(exercise.Type);
            if (contextual && (string.IsNullOrWhiteSpace(exercise.ContextText) || exercise.ContextText.Length > 4000 || exercise.ContextText.Contains('\0')))
                return "Reading and dialogue exercises need a passage or situation up to 4000 characters.";
            if (!contextual && exercise.ContextText != null) return "This exercise type cannot contain a separate context.";
            if (TextEvaluationRulesCodec.Read(exercise.EvaluationRules) is null) return "Choose valid text evaluation rules.";
            if (string.IsNullOrWhiteSpace(exercise.Prompt) || string.IsNullOrWhiteSpace(exercise.Explanation)) return "Every exercise needs a prompt and explanation.";
            var choices = options.Where(item => item.ExerciseId == exercise.Id).ToArray();
            var accepted = answers.Where(item => item.ExerciseId == exercise.Id).ToArray();
            if (exercise.Type is not ("Matching" or "Speaking") && (accepted.Length is < 1 or > 20 || accepted.Any(item => string.IsNullOrWhiteSpace(item.AnswerText)))) return "Every exercise needs valid accepted answers.";
            if (exercise.Type == "Speaking" && (choices.Length != 0 || accepted.Length != 0 || exercise.AudioMediaId is null))
                return "Speaking practice needs reviewed reference audio and no scored choices or answer keys.";
            if (exercise.Type == "SpeakingCheck" && (choices.Length != 0 || exercise.AudioMediaId is null || accepted.Any(item => item.AnswerText.Length > 1000)))
                return "Spoken word checks need reviewed reference audio, accepted phrases up to 1000 characters and no choices.";
            if (exercise.Type == "SpeakingCheck" && courseLanguage.Code is not ("es" or "it" or "de" or "ru"))
                return "Local spoken word checks support Spanish, Italian, German and Russian.";
            if (exercise.Type == "Matching" && (accepted.Length != 0 || MatchingAnswerKeys.Read(choices) is null || exercise.AudioMediaId != null))
                return "Matching needs 2–10 unambiguous pairs, with recordings only on individual left-side items.";
            if (choices.Any(item => item.AudioMediaId != null) && exercise.Type != "Matching") return "Individual option recordings are supported only for matching.";
            var optionAudioIds = choices.Where(item => item.AudioMediaId.HasValue).Select(item => item.AudioMediaId!.Value).ToArray();
            if (optionAudioIds.Any(id => !reviewedMedia.TryGetValue(id, out var clip) || clip.DurationMilliseconds > 30000)
                || optionAudioIds.Select(id => reviewedMedia[id].Sha256).Distinct().Count() != optionAudioIds.Length)
                return "Matching recordings must be reviewed for this language, distinct and at most 30 seconds.";
            if (exercise.Type == "Alphabet" && exercise.AudioMediaId != null && choices.Select(option => option.Text.ToUpperInvariant()).Distinct().Count() != choices.Length)
                return "Sound choices must distinguish different letters, not upper/lowercase versions of one letter.";
            if (exercise.Type == "Alphabet" && (courseLanguage.Code != "ru" || choices.Any(option => option.Text.Length != 1
                || !(option.Text[0] is >= 'А' and <= 'я' or 'Ё' or 'ё')))) return "Russian alphabet exercises need individual Russian letters in the Russian course.";
            if (ExerciseFormats.IsChoice(exercise.Type, choices.Length) && (choices.Length is < 2 or > 8 || choices.Select(item => item.Text).Distinct().Count() != choices.Length || choices.Any(item => string.IsNullOrWhiteSpace(item.Text) || item.Text.Length > 2000)
                || accepted.Any(answer => !choices.Any(option => option.Text == answer.AnswerText)))) return "Choice answers must match distinct options (2–8).";
            if ((exercise.Type is "Translation" or "FillBlank" || exercise.Type == "Listening" && choices.Length == 0) && (choices.Length != 0 || accepted.Any(item => item.AnswerText.Length > 1000))) return "Text exercises need answers up to 1000 characters and no options.";
            if (exercise.Type == "Listening" && exercise.AudioMediaId is null) return "Listening exercises need independently reviewed audio.";
            if (exercise.Type == "FillBlank" && exercise.Prompt.Split("____", StringSplitOptions.None).Length != 2) return "A fill-blank prompt needs exactly one ____ placeholder.";
            if (exercise.Type == "SentenceBuilder" && (choices.Length is < 2 or > 20 || choices.Any(item => item.Text.Length is < 1 or > 80)
                || accepted.Any(item => SentenceAnswerKeys.Read(item.OrderedTokenIds) is not { } order || !SentenceAnswerKeys.IsSelection(order, choices.Select(option => option.Id).ToArray()))))
                return "Sentence answers must use distinct available tiles in order; unused tiles may be distractors.";
            if (exercise.Type != "SentenceBuilder" && accepted.Any(item => item.OrderedTokenIds != null)) return "This text exercise cannot contain token answer keys.";
            if (exercise.AudioMediaId is { } mediaId && (!reviewedMedia.TryGetValue(mediaId, out var clip)
                || exercise.Type is "Speaking" or "SpeakingCheck" or "Alphabet" && clip.DurationMilliseconds > 30000))
                return "Audio must pass independent review for this language; speaking samples must be at most 30 seconds.";
        }
        return null;
    }
}
