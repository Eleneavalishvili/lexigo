namespace LanguageLearning.Infrastructure.Administration;

public sealed record AdministrationPage<T>(int Total, int Page, int PageSize, IReadOnlyList<T> Items);
public sealed record ManagedAccountSummary(Guid Id, string? Email, string DisplayName, string Status, bool EmailConfirmed,
    bool TwoFactorEnabled, string[] Roles, int[] LanguageIds);
public sealed record AuditSummary(Guid Id, Guid? ActorId, string Action, string EntityType, string EntityId, string? Reason, string TraceId, DateTimeOffset OccurredAt);
public sealed record LanguageAccessChoice(int Id, string EnglishName);
