using LanguageLearning.Core.Learning;

namespace LanguageLearning.Tests.Learning;

public sealed class HeartRecoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecoveryStartsAtExhaustionAndRestoresAllFiveAtTheExactDeadline()
    {
        var balance = new LearnerHearts();
        for (int index = 0; index < 4; index++) HeartRecovery.Spend(balance, Now);
        Assert.Equal(1, balance.Count);
        Assert.Null(balance.RefillAt);
        HeartRecovery.Spend(balance, Now);
        Assert.Equal(Now.AddHours(1), balance.RefillAt);
        HeartRecovery.Refresh(balance, Now.AddHours(1).AddTicks(-1));
        Assert.Equal(0, balance.Count);
        Assert.Throws<InvalidOperationException>(() => HeartRecovery.Spend(balance, Now.AddMinutes(10)));
        Assert.Equal(Now.AddHours(1), balance.RefillAt);
        HeartRecovery.Refresh(balance, Now.AddHours(1));
        Assert.Equal(5, balance.Count);
        Assert.Null(balance.RefillAt);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(10, true)]
    public void SpeakingGetsOneFreeRetryThenEveryFurtherMissCostsAHeart(int earlierMisses, bool costsHeart) =>
        Assert.Equal(costsHeart, HeartRecovery.SpeakingMissCostsHeart(earlierMisses));

    [Fact]
    public void SpeakingMissCountCannotBeNegative() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HeartRecovery.SpeakingMissCostsHeart(-1));
}
