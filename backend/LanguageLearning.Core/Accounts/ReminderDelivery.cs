namespace LanguageLearning.Core.Accounts;

public sealed class ReminderDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public string Type { get; set; } = "StudyEmail";
    public string Status { get; set; } = "Queued";
    public DateTimeOffset? SentAt { get; set; }
}
