using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public static class AdministrationLocks
{
    // Begin a transaction first. Role changes, grants and refunds share this lock order.
    public static async Task AcquireAsync(LexiGoDbContext database, IEnumerable<Guid> users, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730027001)", cancellationToken);
        foreach (Guid id in users.Distinct().Order())
        {
            var user = await database.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (user is not null) await database.Entry(user).ReloadAsync(cancellationToken);
        }
    }
}
