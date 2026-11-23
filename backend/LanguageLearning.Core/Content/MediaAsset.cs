namespace LanguageLearning.Core.Content;

public sealed class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string StorageKey { get; set; }
    public required string MimeType { get; set; }
    public long ByteSize { get; set; }
    public int? LanguageId { get; set; }
    public required string Source { get; set; }
    public required string License { get; set; }
    public Guid UploadedBy { get; set; }
    public bool IsVerified { get; set; }
    public string StorageState { get; set; } = "Ready";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int? DurationMilliseconds { get; set; }
    public string? Sha256 { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}

