using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using LanguageLearning.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Billing;

[ApiController]
[Route("api/v1")]
[EnableRateLimiting("learning")]
[RequestSizeLimit(2048)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SubscriptionsController(SubscriptionQueries queries, BillingOrderService orders, SubscriptionPaymentService payments) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet("plans"), AllowAnonymous]
    [ProducesResponseType<PlanView[]>(200)]
    public async Task<IActionResult> Plans(CancellationToken cancellationToken) => Ok(await queries.PlansAsync(cancellationToken));
    [HttpGet("me/subscription"), Authorize]
    [ProducesResponseType<SubscriptionView>(200)]
    public async Task<IActionResult> Subscription(CancellationToken cancellationToken) => Ok(await queries.SubscriptionAsync(UserId, cancellationToken));
    [HttpGet("me/entitlements"), Authorize]
    [ProducesResponseType<EntitlementView>(200)]
    public async Task<IActionResult> Entitlements(CancellationToken cancellationToken) => Ok(await queries.EntitlementsAsync(UserId, cancellationToken));
    [HttpPost("me/billing-orders"), Authorize]
    [ProducesResponseType<BillingOrderView>(200)]
    [ProducesResponseType<BillingOrderView>(201)]
    public async Task<IActionResult> Create(CreateSubscriptionOrder request, CancellationToken cancellationToken)
    {
        var result = await orders.CreateAsync(UserId, FamilyId, request.PlanPriceId, request.IdempotencyKey, cancellationToken);
        if (result.Created && result.Value is { } order)
            return CreatedAtAction(nameof(Order), new { id = order.Id }, order);
        return Result(result.Value, result.Missing, result.Error, result.RateLimited);
    }
    [HttpGet("me/billing-orders/{id:guid}"), Authorize]
    [ProducesResponseType<BillingOrderView>(200)]
    public async Task<IActionResult> Order(Guid id, CancellationToken cancellationToken) => await queries.OrderAsync(UserId, id, cancellationToken) is { } value ? Ok(value) : NotFound();
    [HttpPost("me/billing-orders/{id:guid}/cancel"), Authorize]
    [ProducesResponseType<BillingOrderView>(200)]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        var result = await orders.CancelAsync(UserId, FamilyId, id, cancellationToken);
        return Result(result.Value, result.Missing, result.Error, result.RateLimited);
    }
    [HttpGet("me/billing-orders/by-request/{key:guid}"), Authorize]
    [ProducesResponseType<BillingOrderView>(200)]
    public async Task<IActionResult> OrderByRequest(Guid key, CancellationToken cancellationToken) =>
        await queries.OrderByRequestAsync(UserId, key, cancellationToken) is { } value ? Ok(value) : NotFound();
    [HttpPost("me/billing-orders/{id:guid}/payments"), Authorize]
    [ProducesResponseType<PaymentView>(200)]
    public async Task<IActionResult> Pay(Guid id, MakeSubscriptionPayment request, CancellationToken cancellationToken)
    {
        var result = await payments.PayAsync(UserId, FamilyId, id, request.TestCardId, request.IdempotencyKey, request.SimulateTechnicalFailure, cancellationToken);
        return Result(result.Value, result.Missing, result.Error, result.RateLimited);
    }
    [HttpGet("me/payments/{id:guid}"), Authorize]
    [ProducesResponseType<PaymentView>(200)]
    public async Task<IActionResult> Payment(Guid id, CancellationToken cancellationToken) => await queries.PaymentAsync(UserId, id, cancellationToken) is { } value ? Ok(value) : NotFound();
    [HttpGet("me/payments/by-request/{key:guid}"), Authorize]
    [ProducesResponseType<PaymentView>(200)]
    public async Task<IActionResult> PaymentByRequest(Guid key, CancellationToken cancellationToken) =>
        await queries.PaymentByRequestAsync(UserId, key, cancellationToken) is { } value ? Ok(value) : NotFound();
    [HttpGet("me/payments/{id:guid}/receipt"), Authorize]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken cancellationToken)
    {
        var payment = await queries.PaymentAsync(UserId, id, cancellationToken);
        if (payment is not { Status: "Succeeded", ReceiptNumber: not null }) return NotFound();
        var order = await queries.OrderAsync(UserId, payment.OrderId, cancellationToken);
        string text = string.Join('\n', "LEXIGO — PAYMENT RECEIPT", "Private subscription record", payment.ReceiptNumber,
            "Payment: " + payment.Id, "Period: " + order!.Period, "Amount: " + payment.Amount.ToString("F2", CultureInfo.InvariantCulture) + " " + payment.Currency,
            "Issued: " + payment.CompletedAt!.Value.ToString("O", CultureInfo.InvariantCulture));
        return File(Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", payment.ReceiptNumber + ".txt");
    }

    private IActionResult Result(object? value, bool missing, string? error, bool limited)
    {
        if (limited) { Response.Headers.RetryAfter = "600"; return Problem(statusCode: 429, title: "Take a short break before starting more billing operations."); }
        return missing ? NotFound() : error is not null ? Problem(statusCode: 409, title: error) : Ok(value);
    }
}
public sealed record CreateSubscriptionOrder(Guid PlanPriceId, Guid IdempotencyKey);
public sealed record MakeSubscriptionPayment(Guid TestCardId, Guid IdempotencyKey, bool SimulateTechnicalFailure = false);
