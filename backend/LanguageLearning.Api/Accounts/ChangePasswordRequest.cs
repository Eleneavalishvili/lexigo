using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Api.Accounts;

public sealed record ChangePasswordRequest(
    [Required, StringLength(128)] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 12)] string NewPassword);
