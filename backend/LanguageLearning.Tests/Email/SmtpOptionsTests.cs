using LanguageLearning.Infrastructure.Email;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace LanguageLearning.Tests.Email;

public sealed class SmtpOptionsTests
{
    [Theory]
    [InlineData("localhost", 1025, "hello@lexigo.test", null, null, "Development", true)]
    [InlineData("localhost", 1025, "hello@lexigo.test", null, null, "Production", false)]
    [InlineData("smtp-relay.brevo.com", 587, "sender@example.com", "smtp-login", "synthetic-test-key", "Development", true)]
    [InlineData("smtp-relay.brevo.com", 587, "sender@example.com", null, null, "Development", false)]
    [InlineData("smtp-relay.brevo.com", 465, "sender@example.com", "smtp-login", "synthetic-test-key", "Development", false)]
    [InlineData("smtp-relay.brevo.com", 587, "hello@lexigo.test", "smtp-login", "synthetic-test-key", "Development", false)]
    [InlineData("https://smtp-relay.brevo.com/path", 587, "sender@example.com", "smtp-login", "synthetic-test-key", "Development", false)]
    [InlineData("smtp-relay.brevo.com", 587, "invalid-sender", "smtp-login", "synthetic-test-key", "Development", false)]
    public void EnabledDeliveryRequiresAValidTransport(string host, int port, string sender, string? username, string? password, string environment, bool valid)
    {
        var validator = new SmtpOptionsValidator(new TestEnvironment { EnvironmentName = environment });
        var result = validator.Validate(null, new SmtpOptions
        {
            Host = host,
            Port = port,
            SenderAddress = sender,
            Username = username,
            Password = password
        });
        Assert.Equal(valid, result.Succeeded);
        if (!valid) Assert.DoesNotContain("synthetic-test-key", result.FailureMessage ?? "");
    }

    [Fact]
    public void DisabledDeliveryAllowsCredentialsToBePreparedLater()
    {
        var validator = new SmtpOptionsValidator(new TestEnvironment());
        Assert.True(validator.Validate(null, new SmtpOptions { DeliveryEnabled = false, Host = "smtp-relay.brevo.com" }).Succeeded);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "LexiGo.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
