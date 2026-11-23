using LanguageLearning.Core.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LanguageLearning.Infrastructure.Data;

public sealed class RoleSeeder(RoleManager<IdentityRole<Guid>> roleManager)
{
    public async Task SeedAsync()
    {
        foreach (string name in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(name)) continue;
            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(name));
            if (!result.Succeeded) throw new InvalidOperationException("Could not initialize required roles.");
        }
    }
}
