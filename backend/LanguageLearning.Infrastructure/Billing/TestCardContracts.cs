namespace LanguageLearning.Infrastructure.Billing;

public sealed record TestCardView(Guid Id, string Nickname, string DisplayLastFour, string Currency, decimal Balance,
    string Status, long Version, DateTimeOffset CreatedAt, bool IsDefault);
public sealed record CardTransactionView(Guid Id, Guid TestCardId, string Type, decimal Amount, decimal BalanceAfter,
    string Reference, DateTimeOffset CreatedAt, string Currency, string Status = "Succeeded", Guid? PaymentId = null, Guid? HeartRefillPurchaseId = null);
public sealed record BillingActivityView(Guid Id, Guid TestCardId, string Type, decimal Amount, decimal? BalanceAfter,
    string Reference, DateTimeOffset CreatedAt, string Currency, string Status = "Succeeded", Guid? PaymentId = null,
    Guid? HeartRefillPurchaseId = null, Guid? OrderId = null, string? FailureCode = null, string EntryKind = "CardTransaction", Guid? RefundId = null);
public sealed record BillingActivityPage(IReadOnlyList<BillingActivityView> Items, int Page, int PageSize, int TotalCount);
public sealed record CardChange(TestCardView Card, CardTransactionView? Transaction = null);
public sealed record CardOutcome(CardChange? Value = null, bool Missing = false, string? Error = null);
