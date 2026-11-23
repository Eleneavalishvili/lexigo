using System.Text.Json;

namespace LanguageLearning.Infrastructure.Content;

public static class SentenceAnswerKeys
{
    public static Guid[]? Read(string? json)
    {
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<Guid[]>(json); }
        catch (JsonException) { return null; }
    }

    public static bool IsPermutation<T>(T[] order, T[] tokens) where T : notnull =>
        order.Length == tokens.Length && order.Distinct().Count() == tokens.Length && order.ToHashSet().SetEquals(tokens);

    public static bool IsSelection<T>(T[] order, T[] tokens) where T : notnull =>
        order.Length > 0 && order.Length <= tokens.Length && order.Distinct().Count() == order.Length
        && order.All(tokens.Contains);

    public static bool ValidPositions(int[][] orders, int count) => orders.Length is >= 1 and <= 20
        && orders.All(order => order is not null && IsSelection(order, Enumerable.Range(1, count).ToArray()));

    public static bool Matches(Guid[] order, IReadOnlyDictionary<Guid, string> tokens, Guid[][] accepted) =>
        IsSelection(order, tokens.Keys.ToArray()) && accepted.Any(key => IsSelection(key, tokens.Keys.ToArray())
            && order.Select(id => tokens[id]).SequenceEqual(key.Select(id => tokens[id]), StringComparer.Ordinal));
}
