using System.Net;
using LanguageLearning.Core.Billing;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Billing;

public sealed partial class RefundTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaffDeletionErasesAuthoredRefundNotesWithoutChangingTheActiveBuyer(bool confirmed)
    {
        using var buyer = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var staff = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var keeper = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var customer = await Setup(buyer);
        var actor = await Actor(staff);
        var replacement = await Actor(keeper);
        var refund = await Read<RefundView>(await Post(staff, actor.Token, customer.Payment.Id, 1m));
        var otherRefund = await Read<RefundView>(await Post(keeper, replacement.Token, customer.Payment.Id, 1m));

        using var response = await staff.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = confirmed ? SecurityTestAccount.Password : "Wrong password",
            confirmation = "DELETE", recoveryCode = actor.User.RecoveryCodes[1]
        }, actor.Token, csrf: true);
        Assert.Equal(confirmed ? HttpStatusCode.NoContent : HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var saved = await database.Refunds.AsNoTracking().SingleAsync(item => item.Id == refund.Id);
        Assert.Equal(confirmed ? Refund.ErasedReason : refund.Reason, saved.Reason);
        Assert.Equal(refund.Amount, saved.Amount);
        Assert.Equal(actor.User.Id, saved.CreatedBy);
        Assert.Equal(64, saved.ReasonFingerprint.Length);
        Assert.Equal(otherRefund.Reason, await database.Refunds.Where(item => item.Id == otherRefund.Id)
            .Select(item => item.Reason).SingleAsync());
        Assert.Equal("Active", await database.Users.Where(item => item.Id == customer.User.Id)
            .Select(item => item.Status).SingleAsync());
        Assert.True(await database.CardTransactions.AnyAsync(item => item.RefundId == refund.Id));
        Assert.Equal(saved.Reason, await database.AuditLogs.Where(item => item.EntityType == "Refund"
            && item.EntityId == refund.Id.ToString()).Select(item => item.Reason).SingleAsync());
        var amountChange = await Assert.ThrowsAsync<PostgresException>(() => database.Refunds
            .Where(item => item.Id == refund.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Amount, 2m)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, amountChange.SqlState);
        var noteChange = await Assert.ThrowsAsync<PostgresException>(() => database.Refunds
            .Where(item => item.Id == refund.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Reason, "Restored note")));
        Assert.Equal(PostgresErrorCodes.CheckViolation, noteChange.SqlState);
        if (confirmed)
        {
            using var revoked = await staff.SendAsync(HttpMethod.Get, "/api/v1/me", bearer: actor.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
            var later = await Read<RefundView>(await Post(keeper, replacement.Token, customer.Payment.Id, 1m));
            Assert.NotEqual(Refund.ErasedReason, later.Reason);
        }
    }
}
