using System.Text;
using LanguageLearning.Core.Content;

namespace LanguageLearning.Infrastructure.Content;

public static class MatchingAnswerKeys
{
    private static bool ValidText(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 300 && !text.Any(char.IsControl);
    private static string Label(string text) => string.Join(" ", text.Normalize(NormalizationForm.FormC)
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static bool ValidInput(MatchingPairInput[] pairs) => pairs.Length is >= 2 and <= 10
        && pairs.All(pair => pair is not null && ValidText(pair.Left) && ValidText(pair.Right) && pair.LeftAudioMediaId != Guid.Empty)
        && pairs.Select(pair => Label(pair.Left)).Distinct(StringComparer.Ordinal).Count() == pairs.Length
        && pairs.Select(pair => Label(pair.Right)).Distinct(StringComparer.Ordinal).Count() == pairs.Length
        && pairs.Where(pair => pair.LeftAudioMediaId.HasValue).Select(pair => pair.LeftAudioMediaId).Distinct().Count() == pairs.Count(pair => pair.LeftAudioMediaId.HasValue);

    public static Dictionary<Guid, Guid>? Read(ExerciseOption[] options)
    {
        if (options.Length is < 4 or > 20 || options.Length % 2 != 0) return null;
        var ordered = options.OrderBy(item => item.OrderNumber).ToArray();
        if (!ordered.Select(item => item.OrderNumber).SequenceEqual(Enumerable.Range(1, options.Length))
            || ordered.Any(item => !ValidText(item.Text) || string.IsNullOrWhiteSpace(item.MatchKey))) return null;
        int count = options.Length / 2;
        var left = ordered.Take(count).ToArray();
        var right = ordered.Skip(count).ToArray();
        if (left.Select(item => item.MatchKey).Distinct().Count() != count || right.Select(item => item.MatchKey).Distinct().Count() != count
            || !left.Select(item => item.MatchKey).ToHashSet().SetEquals(right.Select(item => item.MatchKey))
            || left.Select(item => Label(item.Text)).Distinct().Count() != count || right.Select(item => Label(item.Text)).Distinct().Count() != count) return null;
        if (right.Any(item => item.AudioMediaId != null)
            || left.Where(item => item.AudioMediaId.HasValue).Select(item => item.AudioMediaId).Distinct().Count() != left.Count(item => item.AudioMediaId.HasValue)) return null;
        return left.ToDictionary(item => item.Id, item => right.Single(target => target.MatchKey == item.MatchKey).Id);
    }
}
