using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Api.Accounts;

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(80, MinimumLength = 2)] string DisplayName,
    [Required, StringLength(128, MinimumLength = 12)] string Password);
