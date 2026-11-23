namespace LanguageLearning.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string SenderAddress { get; set; } = "hello@lexigo.test";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool DeliveryEnabled { get; set; } = true;
}
