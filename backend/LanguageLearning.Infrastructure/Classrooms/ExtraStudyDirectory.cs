using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record ExtraStudyTeacher(Guid Id, string DisplayName);
public sealed record ExtraStudyAvailability(Guid CourseId, string CourseTitle, bool Enabled);
public enum ExtraStudyWriteResult { Saved, Forbidden, CourseUnavailable }

public sealed class ExtraStudyDirectory(
    LexiGoDbContext database, ClassroomAccess access, SubscriptionQueries subscriptions)
{
    public async Task<ExtraStudyTeacher[]?> FindAsync(
        Guid learnerId, Guid familyId, Guid courseId, Guid? after, CancellationToken cancellationToken)
    {
        if (await access.ActorAsync(learnerId, familyId, false, cancellationToken) is not { CanLearn: true } ||
            (await subscriptions.CurrentAsync(learnerId, cancellationToken))?.PlanId != BillingCatalogSeed.Premium)
            return null;

        var query = VisibleOffers(learnerId).Where(offer => offer.CourseId == courseId);
        if (after is { } cursor) query = query.Where(offer => offer.Id.CompareTo(cursor) > 0);
        return await query.OrderBy(offer => offer.Id).Take(24)
            .Select(offer => new ExtraStudyTeacher(offer.Id, offer.DisplayName)).ToArrayAsync(cancellationToken);
    }

    internal IQueryable<VisibleExtraStudyOffer> VisibleOffers(Guid learnerId) =>
            from offer in database.ExtraStudyOffers.AsNoTracking()
            join teacher in database.Users on offer.TeacherId equals teacher.Id
            join course in database.Courses on offer.CourseId equals course.Id
            where course.IsPublished && offer.Enabled
                && teacher.Id != learnerId && teacher.Status == "Active"
                && teacher.EmailConfirmed && teacher.TwoFactorEnabled
                && (from membership in database.UserRoles
                    join role in database.Roles on membership.RoleId equals role.Id
                    where membership.UserId == teacher.Id && role.Name == RoleNames.Teacher
                    select membership.UserId).Any()
            select new VisibleExtraStudyOffer { Id = offer.Id, CourseId = offer.CourseId, DisplayName = teacher.DisplayName };

    internal sealed class VisibleExtraStudyOffer
    {
        public Guid Id { get; init; }
        public Guid CourseId { get; init; }
        public required string DisplayName { get; init; }
    }

    public async Task<ExtraStudyAvailability[]?> OwnAsync(
        Guid teacherId, Guid familyId, CancellationToken cancellationToken)
    {
        if (await access.ActorAsync(teacherId, familyId, false, cancellationToken) is not { CanTeach: true })
            return null;
        return await (
            from offer in database.ExtraStudyOffers.AsNoTracking()
            join course in database.Courses on offer.CourseId equals course.Id
            where offer.TeacherId == teacherId
            orderby course.Title, course.Id
            select new ExtraStudyAvailability(course.Id, course.Title, offer.Enabled && course.IsPublished)
        ).ToArrayAsync(cancellationToken);
    }

    public async Task<ExtraStudyWriteResult> SetAsync(
        Guid teacherId, Guid familyId, Guid courseId, bool enabled, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await access.ActorAsync(teacherId, familyId, true, cancellationToken) is not { CanTeach: true })
            return ExtraStudyWriteResult.Forbidden;
        if (enabled && !await database.Courses.AnyAsync(course => course.Id == courseId && course.IsPublished, cancellationToken))
            return ExtraStudyWriteResult.CourseUnavailable;

        var offer = await database.ExtraStudyOffers.SingleOrDefaultAsync(
            item => item.TeacherId == teacherId && item.CourseId == courseId, cancellationToken);
        if (offer is not null)
            offer.Enabled = enabled;
        else if (enabled)
            database.ExtraStudyOffers.Add(new ExtraStudyOffer { TeacherId = teacherId, CourseId = courseId, Enabled = true });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ExtraStudyWriteResult.Saved;
    }
}
