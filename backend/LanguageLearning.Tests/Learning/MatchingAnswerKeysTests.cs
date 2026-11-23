using System.Text.Json;
using FluentAssertions;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Learning;

namespace LanguageLearning.Tests.Learning;

public sealed class MatchingAnswerKeysTests
{
    [Theory]
    [InlineData("café", "cafe\u0301", false)]
    [InlineData("Hola", " HOLA ", false)]
    [InlineData("si", "sí", true)]
    public void RejectsAmbiguousLabelsWithoutRemovingMeaningfulAccents(string first, string second, bool valid) =>
        MatchingAnswerKeys.ValidInput([new(first, "First meaning"), new(second, "Second meaning")]).Should().Be(valid);

    [Fact]
    public void OlderSnapshotsStillDecodeWithNoMatchingKeys()
    {
        const string json = """{"answers":["Hola"],"correctOptionIds":[],"feedback":"Greeting","ignoreCase":true,"ignoreTerminalPunctuation":false}""";
        var snapshot = JsonSerializer.Deserialize<EvaluationSnapshot>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        snapshot.Answers.Should().ContainSingle("Hola"); snapshot.MatchingPairs.Should().BeNull(); snapshot.AcceptedTokenOrders.Should().BeNull();
    }
}
