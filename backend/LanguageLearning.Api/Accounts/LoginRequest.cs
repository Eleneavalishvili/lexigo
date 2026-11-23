using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Api.Accounts;

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password,
    [StringLength(12)] string? AuthenticatorCode = null,
    [StringLength(32)] string? RecoveryCode = null);
