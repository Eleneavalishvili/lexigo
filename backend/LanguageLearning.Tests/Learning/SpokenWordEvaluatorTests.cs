using LanguageLearning.Infrastructure.Learning;

namespace LanguageLearning.Tests.Learning;

public sealed class SpokenWordEvaluatorTests
{
    [Theory]
    [InlineData("hola", "¡Hola!", true)]
    [InlineData("ciao", "Ciao.", true)]
    [InlineData("guten  morgen", "Guten Morgen", true)]
    [InlineData("привет", "Привет!", true)]
    [InlineData("adiós", "hola", false)]
    [InlineData("sí", "si", false)]
    [InlineData("е", "ё", false)]
    [InlineData("buon giorno", "buongiorno", false)]
    public void IgnoresUnspokenPunctuationAndCaseButPreservesWordsAndMeaningfulLetters(string recognized, string expected, bool matches) =>
        Assert.Equal(matches, SpokenWordEvaluator.Matches(recognized, expected));
}
