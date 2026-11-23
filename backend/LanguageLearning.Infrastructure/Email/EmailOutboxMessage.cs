namespace LanguageLearning.Infrastructure.Email;

public sealed class EmailOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Purpose { get; set; } = "EmailConfirmation";
    public Guid UserId { get; set; }
    public required string Recipient { get; set; }
    public required string Subject { get; set; }
    public required string HtmlBody { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}
