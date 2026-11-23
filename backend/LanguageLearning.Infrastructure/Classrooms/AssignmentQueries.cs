using System.Data;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record AssignmentSummary(Guid Id, string Title, DateTimeOffset DueAt, int MinimumScore);
public sealed record AssignedLessonView(Guid Id, string Title, bool IsPublished);
public sealed record AssignmentAttempt(Guid LessonId, Guid SessionId, int Score, DateTimeOffset CompletedAt);
public sealed record SubmittedAssignmentAttempt(Guid LessonId, int Score, DateTimeOffset SubmittedAt, bool IsLate);
public sealed record AssignmentLearnerWork(AssignmentAttempt[] Available, SubmittedAssignmentAttempt[] Submitted,
    LearnerFeedbackView[] Feedback);
public sealed record AssignmentDetail(Guid Id, Guid ClassroomId, string Title, DateTimeOffset DueAt,
    int MinimumScore, int Revision, AssignedLessonView[] Lessons, AssignmentLearnerWork? LearnerWork);

public sealed class AssignmentQueries(LexiGoDbContext database, ClassroomAccess access)
{
    public async Task<ClassPage<AssignmentSummary>?> ListAsync(Guid userId, Guid familyId, Guid classId,
        int page, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await access.ActorAsync(userId, familyId, false, cancellationToken);
        if (actor is null || !await VisibleClass(actor, classId).AnyAsync(cancellationToken)) return null;

        const int pageSize = 20;
        var assignments = database.Assignments.AsNoTracking().Where(item => item.ClassroomId == classId);
        var count = await assignments.CountAsync(cancellationToken);
        var items = await assignments.OrderBy(item => item.DueAt).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new AssignmentSummary(item.Id, item.Title, item.DueAt, item.MinimumScore))
            .ToArrayAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(items, page, pageSize, count);
    }

    public async Task<AssignmentDetail?> DetailAsync(Guid userId, Guid familyId, Guid id,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await access.ActorAsync(userId, familyId, false, cancellationToken);
        if (actor is null) return null;

        var assignment = await database.Assignments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (assignment is null) return null;
        var classroom = await VisibleClass(actor, assignment.ClassroomId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (classroom is null) return null;

        var lessons = await (from link in database.AssignmentLessons
                             join lesson in database.Lessons on link.LessonId equals lesson.Id
                             join unit in database.Units on lesson.UnitId equals unit.Id
                             join course in database.Courses on unit.CourseId equals course.Id
                             where link.AssignmentId == id
                             orderby unit.OrderNumber, lesson.OrderNumber, lesson.Id
                             select new AssignedLessonView(lesson.Id,
                                 lesson.Status == "Published" && course.IsPublished ? lesson.Title : "Unavailable lesson",
                                 lesson.Status == "Published" && lesson.CurrentVersionId != null && course.IsPublished))
            .ToArrayAsync(cancellationToken);
        AssignmentLearnerWork? learnerWork = null;
        if (!actor.CanTeach || classroom.TeacherId != userId)
        {
            var joinedAt = await database.ClassMemberships.Where(item => item.ClassroomId == classroom.Id
                && item.UserId == userId && item.Status == "Active").Select(item => item.JoinedAt).SingleAsync(cancellationToken);
            learnerWork = await LearnerWorkAsync(userId, assignment, classroom, joinedAt, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(id, assignment.ClassroomId, assignment.Title, assignment.DueAt, assignment.MinimumScore,
            assignment.Revision, lessons, learnerWork);
    }

    private async Task<AssignmentLearnerWork> LearnerWorkAsync(Guid userId, Core.Classrooms.Assignment assignment,
        Core.Classrooms.Classroom classroom, DateTimeOffset joinedAt, CancellationToken cancellationToken)
    {
        var links = database.AssignmentLessons.Where(item => item.AssignmentId == assignment.Id);
        var submitted = from item in database.AssignmentSubmissions
                        join session in database.LearningSessions on item.SessionId equals session.Id
                        where item.AssignmentId == assignment.Id && item.UserId == userId
                        select new { item.Id, session.LessonId, session.Score, item.SubmittedAt, item.IsLate };
        var latest = await (from link in links
                            from item in submitted.Where(item => item.LessonId == link.LessonId)
                                .OrderByDescending(item => item.SubmittedAt).ThenBy(item => item.Id).Take(1)
                            select new SubmittedAssignmentAttempt(link.LessonId, item.Score, item.SubmittedAt, item.IsLate))
            .ToArrayAsync(cancellationToken);
        var feedback = await (from item in submitted
                              join note in database.TeacherFeedback on item.Id equals note.SubmissionId
                              where item.LessonId != null
                              orderby note.CreatedAt descending, note.Id
                              select new LearnerFeedbackView(item.LessonId!.Value, note.Message, note.CreatedAt, item.Score))
            .Take(20).ToArrayAsync(cancellationToken);
        if (classroom.IsArchived) return new([], latest, feedback);

        var eligible = AssignmentSubmissions.Eligible(database, assignment.Id, userId, classroom.CourseId, assignment.CreatedAt, joinedAt)
            .Where(session => !database.AssignmentSubmissions.Any(item => item.AssignmentId == assignment.Id
                && item.UserId == userId && item.SessionId == session.Id));
        var available = await (from link in links
                               from session in eligible.Where(session => session.LessonId == link.LessonId)
                                   .OrderByDescending(session => session.CompletedAt).ThenBy(session => session.Id).Take(1)
                               select new AssignmentAttempt(link.LessonId, session.Id, session.Score, session.CompletedAt!.Value))
            .ToArrayAsync(cancellationToken);
        return new(available, latest, feedback);
    }

    private IQueryable<Core.Classrooms.Classroom> VisibleClass(ClassActor actor, Guid classId) =>
        database.Classrooms.Where(item => item.Id == classId && (actor.CanTeach && item.TeacherId == actor.Id
            || actor.CanLearn && database.ClassMemberships.Any(member =>
                member.ClassroomId == item.Id && member.UserId == actor.Id && member.Status == "Active")));
}
