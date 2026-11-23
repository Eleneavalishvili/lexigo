using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class LeaderboardParticipationService(LexiGoDbContext database, LearningAccess access)
{
    public async Task<bool> SetAsync(Guid userId, Guid familyId, bool optedIn, bool? sharePhoto,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return false;
        var preference = await database.UserPreferences.SingleAsync(item => item.UserId == userId, cancellationToken);
        preference.LeaderboardOptIn = optedIn;
        if (!optedIn || sharePhoto is false)
        {
            preference.SharedLeaderboardAvatarId = null;
        }
        else if (sharePhoto is true)
        {
            preference.SharedLeaderboardAvatarId = await database.Users.Where(user => user.Id == userId)
                .Select(user => user.AvatarMediaId).SingleAsync(cancellationToken);
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
