using System.Text;
using Microsoft.AspNetCore.Identity;

namespace LanguageLearning.Infrastructure.Identity;

public sealed class PasswordSymbolValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        // Identity's non-alphanumeric rule accepts whitespace; our symbol rule does not.
        if (password is not null)
            foreach (var rune in password.EnumerateRunes())
                if (Rune.IsPunctuation(rune) || Rune.IsSymbol(rune))
                    return Task.FromResult(IdentityResult.Success);

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "PasswordRequiresSymbol",
            Description = "Passwords must contain at least one symbol, such as !, @ or #."
        }));
    }
}
