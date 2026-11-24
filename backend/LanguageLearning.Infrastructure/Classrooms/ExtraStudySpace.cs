using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record SharedStudyLesson(Guid Id, string Title, DateTimeOffset? ScheduledAt = null,
    int? DurationMinutes = null, string Status = "NotStarted", int? BestScore = null, DateTimeOffset? CompletedAt = null);
public sealed record ExtraStudySpaceView(
    Guid CourseId, string CourseTitle, bool CanShare, SharedStudyLesson[] Lessons, string PartnerName = "");

public sealed partial class ExtraStudySpace(
    LexiGoDbContext database, ClassroomAccess access, SubscriptionQueries subscriptions, TimeProvider clock)
{
    private const int LessonLimit = 100;

    public async Task<ExtraStudySpaceView?> ReadAsync(
        Guid userId, Guid familyId, Guid requestId, CancellationToken cancellationToken)
    {
        var space = await AuthorizeAsync(userId, familyId, requestId, false, cancellationToken);
        if (space is null) return null;

        var lessons = await (
            from shared in database.ExtraStudyLessons.AsNoTracking()
            join lesson in PublishedLessons(space.CourseId) on shared.LessonId equals lesson.Id
            join progress in database.LessonProgress.Where(item => item.UserId == space.LearnerId)
                on lesson.Id equals progress.LessonId into learnerProgress
            from progress in learnerProgress.DefaultIfEmpty()
            where shared.RequestId == requestId
            orderby shared.SharedAt, shared.LessonId
            select new SharedStudyLesson(lesson.Id, lesson.Title, shared.ScheduledAt, shared.DurationMinutes,
                progress == null ? "NotStarted" : progress.Status, progress == null ? null : progress.BestScore,
                progress == null ? null : progress.CompletedAt)
        ).Take(LessonLimit).ToArrayAsync(cancellationToken);

        return new(space.CourseId, space.CourseTitle, space.TeacherId == userId, lessons,
            space.TeacherId == userId ? space.LearnerName : space.TeacherName);
    }

    public async Task<ClassroomOutcome> ShareAsync(
        Guid userId, Guid familyId, Guid requestId, Guid lessonId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var space = await AuthorizeAsync(userId, familyId, requestId, true, cancellationToken);
        if (space is null) return new(Missing: true);
        if (await ExtraStudyRequests.LockRequestAsync(database, requestId, cancellationToken) is not { Status: "Accepted" })
            return new(Missing: true);
        if (!await PublishedLessons(space.CourseId).AnyAsync(item => item.Id == lessonId, cancellationToken))
            return new(Missing: true);

        var shared = database.ExtraStudyLessons.Where(item => item.RequestId == requestId);
        if (await shared.AnyAsync(item => item.LessonId == lessonId, cancellationToken)) return new(lessonId);
        if (await shared.CountAsync(cancellationToken) >= LessonLimit)
            return new(Error: "This study space already has 100 shared lessons.");

        database.ExtraStudyLessons.Add(new ExtraStudyLesson
        {
            RequestId = requestId,
            LessonId = lessonId,
            SharedAt = clock.GetUtcNow(),
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(lessonId);
    }

    private async Task<SpaceAccess?> AuthorizeAsync(
        Guid userId, Guid familyId, Guid requestId, bool write, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(userId, familyId, write, cancellationToken);
        if (actor is null || (write && !actor.CanTeach)) return null;

        var space = await (
            from request in database.ExtraStudyRequests.AsNoTracking()
            join offer in database.ExtraStudyOffers on request.OfferId equals offer.Id
            join course in database.Courses on offer.CourseId equals course.Id
            join teacher in database.Users on offer.TeacherId equals teacher.Id
            join learner in database.Users on request.LearnerId equals learner.Id
            where request.Id == requestId && request.Status == "Accepted" && course.IsPublished
                && ((actor.CanTeach && offer.TeacherId == userId) || (!write && actor.CanLearn && request.LearnerId == userId))
                && teacher.Status == "Active" && teacher.EmailConfirmed && teacher.TwoFactorEnabled
                && learner.Status == "Active" && learner.EmailConfirmed
                && (from membership in database.UserRoles
                    join role in database.Roles on membership.RoleId equals role.Id
                    where membership.UserId == teacher.Id && role.Name == "Teacher"
                    select role.Id).Any()
            select new SpaceAccess(course.Id, course.Title, teacher.Id, learner.Id, teacher.DisplayName, learner.DisplayName)
        ).SingleOrDefaultAsync(cancellationToken);
        return space is not null &&
            (await subscriptions.CurrentAsync(space.LearnerId, cancellationToken))?.PlanId == BillingCatalogSeed.Premium
            ? space : null;
    }

    private IQueryable<PublishedStudyLesson> PublishedLessons(Guid courseId) =>
        from lesson in database.Lessons.AsNoTracking()
        join unit in database.Units on lesson.UnitId equals unit.Id
        join version in database.LessonVersions on lesson.CurrentVersionId equals version.Id
        where unit.CourseId == courseId && lesson.Status == "Published"
            && version.Status == "Published" && version.LessonId == lesson.Id
        select new PublishedStudyLesson { Id = lesson.Id, Title = version.Title };

    private sealed record SpaceAccess(Guid CourseId, string CourseTitle, Guid TeacherId, Guid LearnerId,
        string TeacherName, string LearnerName);

    private sealed class PublishedStudyLesson
    {
        public Guid Id { get; init; }
        public required string Title { get; init; }
    }
}
