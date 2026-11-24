using System.Globalization;
using System.Text.Encodings.Web;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class PurchaseReceiptNotice(LexiGoDbContext database, IOptions<AccountEmailOptions> options)
{
    // Called inside the account-locked purchase transaction, after successful payment
    // artifacts are recorded. A replay returns the earlier result before reaching here.
    public async Task QueueAsync(PaymentAttempt payment, Subscription subscription, string receiptNumber,
        CancellationToken token)
    {
        var recipient = await database.Users.Where(user => user.Id == payment.UserId
            && user.Status == "Active" && user.EmailConfirmed).Select(user => user.Email).SingleAsync(token);
        if (recipient is null) return;
        string url = options.Value.WebOrigin.TrimEnd('/') + "/billing/receipt/" + payment.Id;
        string amount = payment.Amount.ToString("0.00", CultureInfo.InvariantCulture) + " " + payment.Currency;
        string end = subscription.CurrentPeriodEnd.ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);
        database.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            UserId = payment.UserId,
            Recipient = recipient,
            Purpose = "BillingReceipt",
            Subject = "Your LexiGo Premium purchase is successful",
            CreatedAt = payment.CompletedAt ?? payment.CreatedAt,
            HtmlBody = $"<h1>Welcome to Premium.</h1><p>Your purchase is successful and Premium is active on your LexiGo account.</p><p>Payment recorded: {HtmlEncoder.Default.Encode(amount)}<br>Receipt: {HtmlEncoder.Default.Encode(receiptNumber)}<br>Access through: {HtmlEncoder.Default.Encode(end)}</p><p><a href=\"{HtmlEncoder.Default.Encode(url)}\">View your receipt and subscription</a></p><p>Your learning progress stays saved when you switch plans.</p>"
        });
        database.Notifications.Add(new Notification
        {
            UserId = payment.UserId,
            Type = "SubscriptionActivated",
            Title = "Premium is active",
            Body = "Your purchase is successful. Your receipt and subscription are available in Billing.",
            CreatedAt = payment.CompletedAt ?? payment.CreatedAt
        });
    }
}
