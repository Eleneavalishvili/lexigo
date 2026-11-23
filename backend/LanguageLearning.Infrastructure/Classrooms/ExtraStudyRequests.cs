using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record ExtraStudyRequestView(
    Guid Id, Guid OfferId, string CourseTitle, string DisplayName, bool Incoming, string Status);

public sealed class ExtraStudyRequests(
    LexiGoDbContext database, ClassroomAccess access, ExtraStudyDirectory directory,
    SubscriptionQueries subscriptions, TimeProvider clock)
{
    public async Task<ExtraStudyRequestView[]?> ListAsync(
        Guid userId, Guid familyId, Guid? after, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(userId, familyId, false, cancellationToken);
        if (actor is null || (!actor.CanLearn && !actor.CanTeach)) return null;

        var query =
            from request in database.ExtraStudyRequests.AsNoTracking()
            join offer in database.ExtraStudyOffers on request.OfferId equals offer.Id
            join course in database.Courses on offer.CourseId equals course.Id
            join teacher in database.Users on offer.TeacherId equals teacher.Id
            join learner in database.Users on request.LearnerId equals learner.Id
            where (actor.CanTeach && offer.TeacherId == userId) ||
                  (actor.CanLearn && request.LearnerId == userId)
            select new { request.Id, request.OfferId, course.Title, Teacher = teacher.DisplayName,
                Learner = learner.DisplayName, Incoming = offer.TeacherId == userId, request.Status };
        if (after is { } cursor) query = query.Where(item => item.Id.CompareTo(cursor) > 0);
        return await query.OrderBy(item => item.Id).Take(24).Select(item => new ExtraStudyRequestView(
            item.Id, item.OfferId, item.Title, item.Incoming ? item.Learner : item.Teacher,
            item.Incoming, item.Status)).ToArrayAsync(cancellationToken);
    }

    public async Task<ClassroomOutcome> RequestAsync(
        Guid learnerId, Guid familyId, Guid offerId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await access.ActorAsync(learnerId, familyId, true, cancellationToken) is not { CanLearn: true } ||
            !await HasPremiumAsync(learnerId, cancellationToken))
            return new(Forbidden: true);

        if (await LockOfferAsync(offerId, cancellationToken) is null) return new(Missing: true);
        var existing = await database.ExtraStudyRequests.AsNoTracking().SingleOrDefaultAsync(
            item => item.OfferId == offerId && item.LearnerId == learnerId, cancellationToken);
        if (existing is not null) return new(existing.Id);
        if (!await directory.VisibleOffers(learnerId).AnyAsync(offer => offer.Id == offerId, cancellationToken))
            return new(Missing: true);

        var request = new ExtraStudyRequest { OfferId = offerId, LearnerId = learnerId, CreatedAt = clock.GetUtcNow() };
        database.ExtraStudyRequests.Add(request);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(request.Id);
    }

    public async Task<ClassroomOutcome> DecideAsync(
        Guid teacherId, Guid familyId, Guid requestId, bool accept, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await access.ActorAsync(teacherId, familyId, true, cancellationToken) is not { CanTeach: true })
            return new(Forbidden: true);

        var request = await (
            from item in database.ExtraStudyRequests
            join offer in database.ExtraStudyOffers on item.OfferId equals offer.Id
            where item.Id == requestId && offer.TeacherId == teacherId
            select item).SingleOrDefaultAsync(cancellationToken);
        if (request is null || await LockOfferAsync(request.OfferId, cancellationToken) is null)
            return new(Missing: true);
        if (await LockRequestAsync(database, requestId, cancellationToken) is null)
            return new(Missing: true);
        await database.Entry(request).ReloadAsync(cancellationToken);

        var status = accept ? "Accepted" : "Declined";
        if (request.Status == status) return new(request.Id);
        if (request.Status != "Pending") return new(Error: "This request already has a decision.");
        if (accept && (!await HasPremiumAsync(request.LearnerId, cancellationToken) ||
            !await database.Users.AnyAsync(user => user.Id == request.LearnerId && user.Status == "Active" &&
                user.EmailConfirmed, cancellationToken) ||
            !await directory.VisibleOffers(request.LearnerId).AnyAsync(offer => offer.Id == request.OfferId, cancellationToken)))
            return new(Error: "The course, teacher listing, or learner’s Premium access is unavailable.");

        request.Status = status;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(request.Id);
    }

    public async Task<ClassroomOutcome> CloseAsync(
        Guid userId, Guid familyId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(userId, familyId, true, cancellationToken);
        if (actor is null) return new(Missing: true);

        bool participant = await (
            from request in database.ExtraStudyRequests
            join offer in database.ExtraStudyOffers on request.OfferId equals offer.Id
            where request.Id == requestId &&
                ((actor.CanLearn && request.LearnerId == userId) ||
                 (actor.CanTeach && offer.TeacherId == userId))
            select request.Id).AnyAsync(cancellationToken);
        if (!participant) return new(Missing: true);

        var requestToClose = await LockRequestAsync(database, requestId, cancellationToken);
        if (requestToClose is null) return new(Missing: true);
        if (requestToClose.Status == "Closed") return new(requestId);
        if (requestToClose.Status == "Declined") return new(Error: "This request has already been declined.");

        requestToClose.Status = "Closed";
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(requestId);
    }

    internal static Task<ExtraStudyRequest?> LockRequestAsync(
        LexiGoDbContext database, Guid requestId, CancellationToken cancellationToken) =>
        database.ExtraStudyRequests.FromSqlInterpolated(
            $"""SELECT * FROM "ExtraStudyRequests" WHERE "Id" = {requestId} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> HasPremiumAsync(Guid userId, CancellationToken cancellationToken) =>
        (await subscriptions.CurrentAsync(userId, cancellationToken))?.PlanId == BillingCatalogSeed.Premium;

    private Task<ExtraStudyOffer?> LockOfferAsync(Guid offerId, CancellationToken cancellationToken) =>
        database.ExtraStudyOffers.FromSqlInterpolated(
            $"""SELECT * FROM "ExtraStudyOffers" WHERE "Id" = {offerId} FOR UPDATE""")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
}
