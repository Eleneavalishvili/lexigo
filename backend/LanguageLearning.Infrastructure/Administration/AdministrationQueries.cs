using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Administration;

public sealed class AdministrationQueries(LexiGoDbContext database)
{
    public async Task<AdministrationPage<ManagedAccountSummary>> UsersAsync(string? search, string? role, string? status, int page, CancellationToken cancellationToken)
    {
        var query = database.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToUpperInvariant();
            query = query.Where(user => user.NormalizedEmail!.Contains(term) || user.DisplayName.ToUpper().Contains(term));
        }
        if (status is not null) query = query.Where(user => user.Status == status);
        if (role is not null) query = query.Where(user => database.UserRoles.Any(link => link.UserId == user.Id && database.Roles.Any(item => item.Id == link.RoleId && item.Name == role)));
        int total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(user => user.DisplayName).ThenBy(user => user.Id).Skip((page - 1) * 20).Take(20)
            .Select(user => new { user.Id, user.Email, user.DisplayName, user.Status, user.EmailConfirmed, user.TwoFactorEnabled }).ToArrayAsync(cancellationToken);
        var ids = rows.Select(row => row.Id).ToArray();
        var roles = await (from link in database.UserRoles join item in database.Roles on link.RoleId equals item.Id where ids.Contains(link.UserId) select new { link.UserId, item.Name }).ToArrayAsync(cancellationToken);
        var scopes = await database.UserLanguageScopes.AsNoTracking().Where(item => ids.Contains(item.UserId)).ToArrayAsync(cancellationToken);
        return new(total, page, 20, rows.Select(row => new ManagedAccountSummary(row.Id, row.Email, row.DisplayName, row.Status, row.EmailConfirmed, row.TwoFactorEnabled,
            roles.Where(item => item.UserId == row.Id).Select(item => item.Name!).Order().ToArray(),
            scopes.Where(item => item.UserId == row.Id).Select(item => item.LanguageId).Order().ToArray())).ToArray());
    }

    public async Task<AdministrationPage<AuditSummary>> AuditAsync(bool superAdmin, Guid? userId, int page, CancellationToken cancellationToken)
    {
        var query = database.AuditLogs.AsNoTracking().AsQueryable();
        if (!superAdmin) query = query.Where(item => item.Action == "UserStatusChanged");
        if (userId.HasValue) query = query.Where(item => item.EntityType == "User" && item.EntityId == userId.Value.ToString());
        int total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).Skip((page - 1) * 20).Take(20)
            .Select(item => new AuditSummary(item.Id, item.ActorId, item.Action, item.EntityType, item.EntityId, item.Reason, item.TraceId, item.OccurredAt)).ToArrayAsync(cancellationToken);
        return new(total, page, 20, items);
    }

    public async Task<LanguageAccessChoice[]> LanguagesAsync(CancellationToken cancellationToken) =>
        await database.Languages.AsNoTracking().Where(item => item.Code == "es" || item.Code == "it" || item.Code == "de" || item.Code == "ru")
            .OrderBy(item => item.EnglishName).Select(item => new LanguageAccessChoice(item.Id, item.EnglishName)).ToArrayAsync(cancellationToken);
}
