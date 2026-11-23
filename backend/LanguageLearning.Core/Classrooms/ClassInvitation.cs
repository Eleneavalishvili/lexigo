namespace LanguageLearning.Core.Classrooms;

public sealed class ClassInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassroomId { get; set; }
    public required string CodeHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
