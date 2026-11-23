using System.Text;
using System.Text.RegularExpressions;

namespace LanguageLearning.Infrastructure.Learning;

public static partial class TextAnswerEvaluator
{
    public static bool Matches(string answer, string accepted, bool ignoreCase, bool ignoreTerminalPunctuation,
        bool allowUnaccentedVowels = false)
    {
        string input = Normalize(answer, ignoreTerminalPunctuation);
        string expected = Normalize(accepted, ignoreTerminalPunctuation);
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(input, expected, comparison)) return true;
        if (!allowUnaccentedVowels || input.Length != expected.Length) return false;

        for (int index = 0; index < input.Length; index++)
        {
            if (input.AsSpan(index, 1).Equals(expected.AsSpan(index, 1), comparison)) continue;
            if ("aeiouAEIOU".IndexOf(input[index]) < 0 || !char.IsLetter(expected[index])) return false;
            string decomposed = expected[index].ToString().Normalize(NormalizationForm.FormD);
            if (decomposed.Length < 2 || !input.AsSpan(index, 1).Equals(decomposed.AsSpan(0, 1), comparison))
                return false;
        }
        return true;
    }

    private static string Normalize(string value, bool ignoreTerminalPunctuation)
    {
        // Curly and keyboard apostrophes are typographic variants, not missing letters.
        string normalized = Whitespace().Replace(value.Normalize(NormalizationForm.FormC)
            .Replace('’', '\'').Replace('‘', '\'').Trim(), " ");
        return ignoreTerminalPunctuation
            ? normalized.TrimStart('¿', '¡').TrimStart().TrimEnd('.', '!', '?', '…').TrimEnd()
            : normalized;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Whitespace();
}
