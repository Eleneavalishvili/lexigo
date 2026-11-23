namespace LanguageLearning.Infrastructure.Learning;

public sealed record LeaderboardEntry(int Rank, string DisplayName, long Xp, bool IsMe,
    string? PhotoUrl = null, string? ReportToken = null);
public sealed record LeaderboardView(Guid CourseId, string CourseTitle, DateOnly Week, DateOnly CurrentWeek,
    bool OptedIn, long MyXp, LeaderboardEntry? Me, LeaderboardEntry[] Items,
    int TotalCount, int Page, int PageSize, bool PhotoShared = false);

internal sealed record LeaderboardRow(Guid UserId, string DisplayName, long Xp, int Rank);
