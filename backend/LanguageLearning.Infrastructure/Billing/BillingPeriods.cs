using System.Globalization;

namespace LanguageLearning.Infrastructure.Billing;

public static class BillingPeriods
{
    public static DateTimeOffset? Start(string? orderType, string? renewalKey, DateTimeOffset? paidAt) =>
        orderType == "Purchase" ? paidAt : orderType == "Renewal"
            && DateTimeOffset.TryParse(renewalKey, CultureInfo.InvariantCulture, DateTimeStyles.None, out var due) ? due : null;

    public static DateTimeOffset? End(DateTimeOffset? start, string? period) => start is { } date
        ? period == "Monthly" ? date.AddMonths(1) : period == "Annual" ? date.AddYears(1) : null : null;
}
