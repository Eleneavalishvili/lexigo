using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting("account-entry")]
public sealed class RegistrationController(RegistrationService registration) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (request.DisplayName.Trim().Length < 2)
        {
            ModelState.AddModelError(nameof(request.DisplayName), "Enter a name with at least two characters.");
            return ValidationProblem(ModelState);
        }

        var result = await registration.RegisterAsync(request.Email, request.DisplayName, request.Password, cancellationToken);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("account", error.Description);
            return ValidationProblem(ModelState);
        }
        return Accepted(new { message = "If this address can be registered, you will receive a confirmation email. Check your inbox." });
    }

    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Resend(EmailRequest request, CancellationToken cancellationToken)
    {
        await registration.ResendAsync(request.Email, cancellationToken);
        return Accepted(new { message = "If this account needs confirmation, a new email will arrive shortly." });
    }

    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await registration.ConfirmEmailAsync(request.UserId, request.Token, cancellationToken);
        return result switch
        {
            EmailConfirmationResult.Confirmed => Ok(new { message = "Your email is confirmed. Welcome to LexiGo." }),
            EmailConfirmationResult.AlreadyConfirmed => Problem(statusCode: 400, title: "Your email is already confirmed. You can sign in.",
                extensions: new Dictionary<string, object?> { ["code"] = "email_already_confirmed" }),
            _ => Problem(statusCode: 400, title: "This confirmation link is invalid or has expired.",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_confirmation" })
        };
    }
}
