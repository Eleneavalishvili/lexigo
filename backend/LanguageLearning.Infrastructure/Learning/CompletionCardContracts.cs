namespace LanguageLearning.Infrastructure.Learning;

public sealed record CompletionCardDetails(string DisplayName, string CourseTitle, string UnitTitle,
    DateTimeOffset IssuedAt, DateTimeOffset CompletedAt, string VerificationCode,
    int LessonCount, int GradedLessonCount, decimal? AverageBestScore);

public sealed record OwnedCompletionCard(Guid Id, Guid CourseId, Guid UnitId,
    CompletionCardDetails Details, bool IsShared, string? ShareCode, int Revision);
public sealed record CompletionCardPage(OwnedCompletionCard[] Items, int Page, int PageSize, int TotalCount);
public sealed record CompletionCardOutcome(Guid? Id = null, string? Error = null, bool Missing = false,
    bool Created = false);
