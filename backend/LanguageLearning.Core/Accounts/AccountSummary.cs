namespace LanguageLearning.Core.Accounts;

public sealed record AccountSummary(Guid Id, string Email, string DisplayName, bool EmailConfirmed,
    IReadOnlyList<string> Roles, IReadOnlyList<string>? Permissions = null);
