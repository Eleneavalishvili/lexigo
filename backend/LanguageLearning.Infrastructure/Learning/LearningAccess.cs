using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class LearningAccess(LexiGoDbContext database, TimeProvider clock)
{
    public async Task<bool> LockAsync(Guid userId, Guid familyId, CancellationToken cancellationToken)
    {
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return false;
        await database.Entry(user).ReloadAsync(cancellationToken);
        return user.Status == "Active" && user.EmailConfirmed && await database.RefreshSessions.AnyAsync(item =>
            item.UserId == userId && item.FamilyId == familyId && item.RevokedAt == null && item.ExpiresAt > clock.GetUtcNow()
            && item.SecurityStamp == user.SecurityStamp, cancellationToken);
    }
}
