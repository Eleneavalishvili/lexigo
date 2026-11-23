using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class CompletionCardQueries(LexiGoDbContext database)
{
    private IQueryable<CompletionCard> Owned(Guid userId) => database.CompletionCards.AsNoTracking()
        .Where(card => card.UserId == userId && database.Users.Any(user => user.Id == userId && user.Status == "Active"));

    private IQueryable<OwnedCompletionCard> Project(IQueryable<CompletionCard> cards) =>
        from card in cards
        join user in database.Users on card.UserId equals user.Id
        select new OwnedCompletionCard(card.Id, card.CourseId, card.UnitId,
            new(user.DisplayName, card.CourseTitle, card.UnitTitle, card.IssuedAt, card.CompletedAt,
                card.VerificationCode, card.LessonCount, card.GradedLessonCount, card.AverageBestScore),
            card.IsShared, card.ShareCode, card.Revision);

    public Task<OwnedCompletionCard?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        Project(Owned(userId).Where(item => item.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public async Task<CompletionCardPage> ListAsync(Guid userId, Guid? unitId, int page, CancellationToken cancellationToken)
    {
        var query = Owned(userId);
        if (unitId is { } unit) query = query.Where(item => item.UnitId == unit);
        int count = await query.CountAsync(cancellationToken);
        var items = await Project(query.OrderByDescending(item => item.IssuedAt).ThenBy(item => item.Id)
            .Skip((page - 1) * 20).Take(20)).ToArrayAsync(cancellationToken);
        return new(items, page, 20, count);
    }

    public Task<CompletionCardDetails?> SharedAsync(string code, CancellationToken cancellationToken)
    {
        if (code.Length != 32 || code.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            return Task.FromResult<CompletionCardDetails?>(null);
        return (from card in database.CompletionCards.AsNoTracking()
                join user in database.Users on card.UserId equals user.Id
                where card.IsShared && card.ShareCode == code && user.Status == "Active" && user.EmailConfirmed
                select new CompletionCardDetails(user.DisplayName, card.CourseTitle, card.UnitTitle,
                    card.IssuedAt, card.CompletedAt, card.VerificationCode, card.LessonCount, card.GradedLessonCount, card.AverageBestScore))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
