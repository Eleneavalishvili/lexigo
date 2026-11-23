namespace LanguageLearning.Infrastructure.Learning;

public sealed record DashboardView(long TotalXp, int TodayXp, int DailyGoalXp,
    bool GoalReached, int CurrentStreak, int CompletedLessons, DateOnly LocalDate,
    string TimeZone, HeartStatus Hearts, DashboardCourse? Course);

public sealed record DashboardCourse(Guid Id, string Title, string LanguageCode,
    long EarnedXp, int CompletedLessons);
