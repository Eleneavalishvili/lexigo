using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Api.Accounts;

public sealed record ResetPasswordRequest(
    [Required] Guid UserId,
    [Required, StringLength(4096)] string Token,
    [Required, StringLength(128, MinimumLength = 12)] string NewPassword);
