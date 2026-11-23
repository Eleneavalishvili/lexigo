namespace LanguageLearning.Infrastructure.Learning;

public static class AnswerCorrection
{
    // Only call after an owned answer has been submitted; never include this in exercise delivery.
    public static string? Describe(ExerciseView exercise, EvaluationSnapshot evaluation)
    {
        if (evaluation.MatchingPairs is { Length: > 0 } pairs)
            return string.Join("\n", pairs.Select(pair =>
                $"{exercise.Options.Single(item => item.Id == pair.LeftOptionId).Text} — {exercise.Options.Single(item => item.Id == pair.RightOptionId).Text}"));
        if (evaluation.AcceptedTokenOrders?.FirstOrDefault() is { Length: > 0 } tokens)
            return string.Join(" ", tokens.Select(id => exercise.Options.Single(item => item.Id == id).Text));
        if (evaluation.CorrectOptionIds.Length > 0)
            return string.Join(" · ", evaluation.CorrectOptionIds.Select(id => exercise.Options.Single(item => item.Id == id).Text));
        return evaluation.Answers.FirstOrDefault();
    }
}
