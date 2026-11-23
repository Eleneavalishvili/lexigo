using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Api.Accounts;

public sealed record EmailRequest([Required, EmailAddress, StringLength(256)] string Email);
