using LanguageLearning.Infrastructure.Learning;

namespace LanguageLearning.Infrastructure.Billing;

public sealed record HeartRefillOffer(string OfferCode, decimal Amount, string Currency, int MaximumHearts, HeartStatus Hearts);
public sealed record HeartPurchaseView(Guid Id, Guid TestCardId, string OfferCode, string Status, string? FailureCode,
    decimal Amount, decimal ChargedAmount, string Currency, string Reference, int HeartsBefore, int HeartsAfter, DateTimeOffset CreatedAt);
public sealed record HeartPurchasePage(IReadOnlyList<HeartPurchaseView> Items, int Page, int PageSize, int TotalCount);
public sealed record HeartPurchaseOutcome(HeartPurchaseView? Value = null, bool Missing = false, string? Error = null, bool RateLimited = false);
