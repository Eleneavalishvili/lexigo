using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Api.Accounts;

public sealed record ConfirmEmailRequest(
    [Required] Guid UserId,
    [Required, StringLength(4096)] string Token);
