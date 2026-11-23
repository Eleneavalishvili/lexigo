using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class BillingAccountAccess(LexiGoDbContext database, TimeProvider clock)
{
    // Caller begins the transaction first. Every wallet operation uses this user-first lock order.
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
