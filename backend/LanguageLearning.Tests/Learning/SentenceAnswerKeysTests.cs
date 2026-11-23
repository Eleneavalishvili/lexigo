using FluentAssertions;
using LanguageLearning.Infrastructure.Content;

namespace LanguageLearning.Tests.Learning;

public sealed class SentenceAnswerKeysTests
{
    [Fact]
    public void IdenticalWordTilesAreInterchangeableButDifferentWordOrderIsWrong()
    {
        Guid first = Guid.NewGuid(), second = Guid.NewGuid(), third = Guid.NewGuid();
        var words = new Dictionary<Guid, string> { [first] = "very", [second] = "very", [third] = "good" };
        SentenceAnswerKeys.Matches([second, first, third], words, [[first, second, third]]).Should().BeTrue();
        SentenceAnswerKeys.Matches([first, third, second], words, [[first, second, third]]).Should().BeFalse();
        SentenceAnswerKeys.Matches([first, first, third], words, [[first, second, third]]).Should().BeFalse();
        SentenceAnswerKeys.Read("{bad}").Should().BeNull();
    }
    [Fact]
    public void DistractorsCanBeLeftUnusedButUnknownRepeatedOrExtraTilesCannotPass()
    {
        Guid tea = Guid.NewGuid(), and = Guid.NewGuid(), milk = Guid.NewGuid();
        var words = new Dictionary<Guid, string> { [tea] = "tea", [and] = "and", [milk] = "milk" };
        SentenceAnswerKeys.ValidPositions([[1]], 3).Should().BeTrue();
        SentenceAnswerKeys.Matches([tea], words, [[tea]]).Should().BeTrue();
        SentenceAnswerKeys.Matches([milk], words, [[tea]]).Should().BeFalse();
        SentenceAnswerKeys.Matches([tea, and], words, [[tea]]).Should().BeFalse();
        SentenceAnswerKeys.Matches([tea, tea], words, [[tea]]).Should().BeFalse();
        SentenceAnswerKeys.Matches([Guid.NewGuid()], words, [[tea]]).Should().BeFalse();
        SentenceAnswerKeys.Matches([], words, [[tea]]).Should().BeFalse();
    }
}
