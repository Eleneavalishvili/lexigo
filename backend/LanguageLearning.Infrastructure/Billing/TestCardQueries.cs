using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class TestCardQueries(LexiGoDbContext database)
{
    public async Task<IReadOnlyList<TestCardView>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        Guid? defaultId = await database.BillingPreferences.Where(item => item.UserId == userId)
            .Select(item => item.DefaultTestCardId).SingleOrDefaultAsync(cancellationToken);
        return await database.TestCards.AsNoTracking().Where(item => item.UserId == userId).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new TestCardView(item.Id, item.Nickname, item.DisplayLastFour, item.Currency,
                item.Balance, item.Status, item.Version, item.CreatedAt, item.Id == defaultId)).ToArrayAsync(cancellationToken);
    }

    public async Task<BillingActivityPage?> HistoryAsync(Guid userId, Guid? cardId, string? type, string? status,
        DateTimeOffset? from, DateTimeOffset? to, int page, CancellationToken cancellationToken)
    {
        if (cardId is { } id && !await database.TestCards.AnyAsync(item => item.UserId == userId && item.Id == id, cancellationToken)) return null;
        // Declined/pending attempts are read projections, never inserted as balance-changing ledger entries.
        var ledger = from entry in database.CardTransactions.AsNoTracking()
                     join card in database.TestCards on entry.TestCardId equals card.Id
                     where card.UserId == userId
                     join refund in database.Refunds on entry.RefundId equals (Guid?)refund.Id into refunds
                     from refund in refunds.DefaultIfEmpty()
                     join payment in database.PaymentAttempts.Where(item => item.UserId == userId)
                         on (entry.PaymentId ?? (Guid?)refund.PaymentId) equals (Guid?)payment.Id into associatedPayments
                     from payment in associatedPayments.DefaultIfEmpty()
                     select new
                     {
                         entry.Id,
                         entry.TestCardId,
                         entry.Type,
                         entry.Amount,
                         BalanceAfter = (decimal?)entry.BalanceAfter,
                         entry.Reference,
                         entry.CreatedAt,
                         card.Currency,
                         Status = "Succeeded",
                         PaymentId = (Guid?)payment.Id,
                         entry.HeartRefillPurchaseId,
                         OrderId = (Guid?)payment.OrderId,
                         FailureCode = (string?)null,
                         EntryKind = "CardTransaction",
                         entry.RefundId
                     };
        var payments = from payment in database.PaymentAttempts.AsNoTracking()
                       join card in database.TestCards on payment.TestCardId equals card.Id
                       where payment.UserId == userId && card.UserId == userId && payment.Status != "Succeeded"
                       select new
                       {
                           payment.Id,
                           payment.TestCardId,
                           Type = "Debit",
                           payment.Amount,
                           BalanceAfter = (decimal?)null,
                           Reference = "PAY-" + payment.Id.ToString().Replace("-", ""),
                           payment.CreatedAt,
                           payment.Currency,
                           payment.Status,
                           PaymentId = (Guid?)payment.Id,
                           HeartRefillPurchaseId = (Guid?)null,
                           OrderId = (Guid?)payment.OrderId,
                           payment.FailureCode,
                           EntryKind = "PaymentAttempt",
                           RefundId = (Guid?)null
                       };
        var refills = from refill in database.HeartRefillPurchases.AsNoTracking()
                      join card in database.TestCards on refill.TestCardId equals card.Id
                      where refill.UserId == userId && card.UserId == userId && refill.Status != "Succeeded"
                      select new
                      {
                          refill.Id,
                          refill.TestCardId,
                          Type = "Debit",
                          refill.Amount,
                          BalanceAfter = (decimal?)null,
                          refill.Reference,
                          refill.CreatedAt,
                          refill.Currency,
                          refill.Status,
                          PaymentId = (Guid?)null,
                          HeartRefillPurchaseId = (Guid?)refill.Id,
                          OrderId = (Guid?)null,
                          refill.FailureCode,
                          EntryKind = "HeartRefillPurchase",
                          RefundId = (Guid?)null
                      };
        var query = ledger.Concat(payments).Concat(refills);
        if (cardId is { } filter) query = query.Where(item => item.TestCardId == filter);
        if (type is not null) query = query.Where(item => item.Type == type);
        if (status is not null) query = query.Where(item => item.Status == status);
        if (from is { } start) query = query.Where(item => item.CreatedAt >= start.ToUniversalTime());
        if (to is { } end) query = query.Where(item => item.CreatedAt < end.ToUniversalTime());
        int count = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).ThenBy(item => item.EntryKind)
            .Skip((page - 1) * 20).Take(20)
            .Select(entry => new BillingActivityView(entry.Id, entry.TestCardId, entry.Type, entry.Amount,
                entry.BalanceAfter, entry.Reference, entry.CreatedAt, entry.Currency, entry.Status, entry.PaymentId,
                entry.HeartRefillPurchaseId, entry.OrderId, entry.FailureCode, entry.EntryKind, entry.RefundId)).ToArrayAsync(cancellationToken);
        return new(entries, page, 20, count);
    }

    public static CardTransactionView View(CardTransaction entry, string currency) =>
        new(entry.Id, entry.TestCardId, entry.Type, entry.Amount, entry.BalanceAfter, entry.Reference, entry.CreatedAt, currency, "Succeeded", entry.PaymentId, entry.HeartRefillPurchaseId);
}
