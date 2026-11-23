namespace LanguageLearning.Infrastructure.Identity;

public sealed class RefreshSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public required string TokenHash { get; set; }
    public required string SecurityStamp { get; set; }
    public required string DeviceLabel { get; set; }
    public bool MfaVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public Guid? ReplacedById { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
