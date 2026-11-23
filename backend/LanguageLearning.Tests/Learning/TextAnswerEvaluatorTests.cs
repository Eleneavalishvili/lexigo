using FluentAssertions;
using LanguageLearning.Infrastructure.Learning;

namespace LanguageLearning.Tests.Learning;

public sealed class TextAnswerEvaluatorTests
{
    [Theory]
    [InlineData("  HOLA  ", "hola", true, false, true)]
    [InlineData("cafe\u0301", "café", false, false, true)]
    [InlineData("Guten\u00a0  Morgen", "Guten Morgen", false, false, true)]
    [InlineData("schön", "schon", true, false, false)]
    [InlineData("straße", "strasse", true, false, false)]
    [InlineData("Привет", "привет", true, false, true)]
    [InlineData("Hola!", "Hola", true, false, false)]
    [InlineData("Hola!", "Hola", true, true, true)]
    [InlineData("l'acqua", "lacqua", true, true, false)]
    [InlineData("Hola", "hola", false, false, false)]
    [InlineData("Dov'è la stazione?", "Dov’è la stazione?", true, false, true)]
    [InlineData("l‘acqua", "l'acqua", false, false, true)]
    [InlineData("l’acqua", "lacqua", true, true, false)]
    [InlineData("Que hora es", "¿Qué hora es?", true, true, false)]
    [InlineData("Qué hora es", "¿Qué hora es?", true, true, true)]
    [InlineData("Qué hora es?", "¿Qué hora es?", true, false, false)]
    [InlineData("Елка", "Ёлка", true, true, false)]
    [InlineData("schon", "schön", true, true, false)]
    [InlineData("Non posso", "Non, posso", true, true, false)]
    public void AppliesOnlyTheSelectedNormalizationRules(string input, string accepted, bool ignoreCase, bool ignorePunctuation, bool expected) =>
        TextAnswerEvaluator.Matches(input, accepted, ignoreCase, ignorePunctuation).Should().Be(expected);

    [Theory]
    [InlineData("Que hora es", "¿Qué hora es?", true)]
    [InlineData("fur schön", "für schön", true)]
    [InlineData("Dov'e", "Dov’è", true)]
    [InlineData("e", "è", true)]
    [InlineData("é", "è", false)]
    [InlineData("u", "ö", false)]
    [InlineData("si", "sí", true)]
    [InlineData("ano", "año", false)]
    [InlineData("strasse", "Straße", false)]
    [InlineData("Елка", "Ёлка", false)]
    [InlineData("privet", "привет", false)]
    public void KeyboardFallbackAcceptsOnlyMissingMarksOnMatchingLatinVowels(string input, string accepted, bool expected) =>
        TextAnswerEvaluator.Matches(input, accepted, true, true, allowUnaccentedVowels: true)
            .Should().Be(expected);
}
