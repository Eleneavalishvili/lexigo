using System.Net.Mail;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Email;

public sealed class SmtpOptionsValidator(IHostEnvironment environment) : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        if (!options.DeliveryEnabled) return ValidateOptionsResult.Success;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Host) || Uri.CheckHostName(options.Host) == UriHostNameType.Unknown)
            errors.Add("Configure an SMTP hostname without a URL path or scheme.");
        if (options.Port is < 1 or > 65535 || options.Port == 465)
            errors.Add("Configure a STARTTLS SMTP port, such as 587. Implicit TLS on port 465 is not supported by this sender.");
        if (!MailAddress.TryCreate(options.SenderAddress, out var sender) || sender.Address != options.SenderAddress)
            errors.Add("Configure one verified sender email address.");

        bool local = options.Host is "localhost" or "127.0.0.1";
        if (local && !environment.IsDevelopment())
            errors.Add("Local test SMTP is restricted to Development.");
        if (!local && (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password)))
            errors.Add("Configure the SMTP login and SMTP key through secrets before enabling external delivery.");
        if (!local && sender is not null && (sender.Host.EndsWith(".test", StringComparison.OrdinalIgnoreCase)
            || sender.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase)
            || sender.Host.EndsWith(".example", StringComparison.OrdinalIgnoreCase)))
            errors.Add("A test-domain sender cannot be used for external delivery.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
