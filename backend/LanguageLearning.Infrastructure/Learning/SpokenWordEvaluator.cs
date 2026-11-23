using System.Text;

namespace LanguageLearning.Infrastructure.Learning;

public static class SpokenWordEvaluator
{
    public static bool Matches(string recognized, string expected) =>
        TextAnswerEvaluator.Matches(WithoutPunctuation(recognized), WithoutPunctuation(expected), true, false);

    private static string WithoutPunctuation(string text) =>
        new(text.Normalize(NormalizationForm.FormC).Where(character => !char.IsPunctuation(character)).ToArray());
}
