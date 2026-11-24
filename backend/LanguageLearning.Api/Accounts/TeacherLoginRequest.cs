using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Api.Accounts;

public sealed record TeacherLoginRequest(
    [Required, RegularExpression("^[0-9]{6}$")] string AccessCode,
    [Required, RegularExpression("^[0-9]{6}$")] string AuthenticatorCode);
