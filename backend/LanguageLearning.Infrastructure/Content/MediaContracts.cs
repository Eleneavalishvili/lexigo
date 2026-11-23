namespace LanguageLearning.Infrastructure.Content;

public sealed record MediaUploadInput(int LanguageId, string Source, string License);
public sealed record MediaOutcome(Guid? Id = null, string? Error = null, bool Forbidden = false, bool Unavailable = false);
public sealed record StaffMedia(Guid Id, int? LanguageId, string MimeType, long ByteSize, int? DurationMilliseconds,
    string Source, string License, Guid UploadedBy, bool IsVerified, string StorageState, DateTimeOffset CreatedAt, Guid? VerifiedBy, string? ReviewComment);
