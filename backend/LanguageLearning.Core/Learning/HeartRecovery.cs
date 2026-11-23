namespace LanguageLearning.Core.Learning;

public static class HeartRecovery
{
    public const int MaximumHearts = 5;
    public static readonly TimeSpan Wait = TimeSpan.FromHours(1);

    public static void Refresh(LearnerHearts hearts, DateTimeOffset now)
    {
        if (hearts.Count == 0 && hearts.RefillAt <= now)
        {
            hearts.Count = MaximumHearts;
            hearts.RefillAt = null;
        }
    }

    public static void Spend(LearnerHearts hearts, DateTimeOffset now)
    {
        Refresh(hearts, now);
        if (hearts.Count <= 0) throw new InvalidOperationException("No hearts are available.");
        if (--hearts.Count == 0) hearts.RefillAt = now.Add(Wait);
    }

    public static bool SpeakingMissCostsHeart(int earlierUnsuccessfulChecks)
    {
        if (earlierUnsuccessfulChecks < 0) throw new ArgumentOutOfRangeException(nameof(earlierUnsuccessfulChecks));
        return earlierUnsuccessfulChecks >= 1;
    }
}
