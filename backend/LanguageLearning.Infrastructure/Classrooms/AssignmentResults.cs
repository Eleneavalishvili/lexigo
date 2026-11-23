using System.Data;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record AssignmentLessonResult(Guid LessonId, string Title, int AttemptCount, int BestScore,
    Guid LatestSubmissionId, int LatestScore, int LatestAccuracy, DateTimeOffset SubmittedAt, bool IsLate,
    AssignmentFeedbackView? Feedback);
public sealed record AssignmentLearnerResult(Guid LearnerId, string DisplayName, string MembershipStatus,
    string Status, AssignmentLessonResult[] Lessons);
public sealed record AssignmentResultsPage(int MinimumScore, int RequiredLessons, ClassPage<AssignmentLearnerResult> Learners,
    bool IsArchived);

public sealed class AssignmentResults(LexiGoDbContext database, ClassroomAccess access)
{
    public async Task<AssignmentResultsPage?> GetAsync(Guid userId, Guid familyId, Guid id, int page,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await access.ActorAsync(userId, familyId, false, cancellationToken);
        if (actor is not { CanTeach: true }) return null;

        var assignment = await (from item in database.Assignments
                                join classroom in database.Classrooms on item.ClassroomId equals classroom.Id
                                where item.Id == id && classroom.TeacherId == userId
                                select new { item.ClassroomId, item.MinimumScore, classroom.IsArchived })
            .SingleOrDefaultAsync(cancellationToken);
        if (assignment is null) return null;

        var members = database.ClassMemberships.Where(member => member.ClassroomId == assignment.ClassroomId
            && (member.Status == "Active" || database.AssignmentSubmissions.Any(item => item.AssignmentId == id && item.UserId == member.UserId)));
        const int pageSize = 20;
        var count = await members.CountAsync(cancellationToken);
        var roster = await (from member in members
                            join user in database.Users on member.UserId equals user.Id
                            orderby user.DisplayName, user.Id
                            select new { member.UserId, user.DisplayName, member.Status })
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);

        var lessons = await (from link in database.AssignmentLessons
                             join lesson in database.Lessons on link.LessonId equals lesson.Id
                             join unit in database.Units on lesson.UnitId equals unit.Id
                             where link.AssignmentId == id
                             orderby unit.OrderNumber, lesson.OrderNumber, lesson.Id
                             select new { lesson.Id, lesson.Title }).ToArrayAsync(cancellationToken);
        var learnerIds = roster.Select(member => member.UserId).ToArray();
        var submissions = from item in database.AssignmentSubmissions
                          join session in database.LearningSessions on item.SessionId equals session.Id
                          where item.AssignmentId == id && learnerIds.Contains(item.UserId) && session.LessonId != null
                          select new { item.Id, item.UserId, item.SessionId, item.SubmittedAt, item.IsLate, session.LessonId, session.Score };
        var totals = await submissions.GroupBy(item => new { item.UserId, item.LessonId })
            .Select(group => new { group.Key.UserId, group.Key.LessonId, Count = group.Count(), Best = group.Max(item => item.Score) })
            .ToDictionaryAsync(item => (item.UserId, item.LessonId!.Value), cancellationToken);
        var latest = await (from pair in submissions.Select(item => new { item.UserId, item.LessonId }).Distinct()
                            from item in submissions.Where(item => item.UserId == pair.UserId && item.LessonId == pair.LessonId)
                                .OrderByDescending(item => item.SubmittedAt).ThenBy(item => item.Id).Take(1)
                            select item).ToArrayAsync(cancellationToken);
        var sessionIds = latest.Select(item => item.SessionId).ToArray();
        var submissionIds = latest.Select(item => item.Id).ToArray();
        var feedback = await database.TeacherFeedback.Where(item => submissionIds.Contains(item.SubmissionId))
            .ToDictionaryAsync(item => item.SubmissionId, item => new AssignmentFeedbackView(item.Message, item.CreatedAt), cancellationToken);
        var accuracy = await (from exercise in database.SessionExercises
                              join attempt in database.AnswerAttempts on exercise.Id equals attempt.SessionExerciseId
                              where sessionIds.Contains(exercise.SessionId) && attempt.IsGraded
                              group attempt by exercise.SessionId into attempts
                              select new { SessionId = attempts.Key, Correct = attempts.Count(attempt => attempt.IsCorrect), Total = attempts.Count() })
            .ToDictionaryAsync(item => item.SessionId, item => (int)(item.Correct * 100L / item.Total), cancellationToken);
        var byLearnerAndLesson = latest.ToDictionary(item => (item.UserId, item.LessonId!.Value));
        var results = roster.Select(member =>
        {
            var submittedLessons = new List<AssignmentLessonResult>();
            foreach (var lesson in lessons)
            {
                if (!byLearnerAndLesson.TryGetValue((member.UserId, lesson.Id), out var item)) continue;
                var total = totals[(member.UserId, lesson.Id)];
                submittedLessons.Add(new(lesson.Id, lesson.Title, total.Count, total.Best, item.Id, item.Score,
                    accuracy[item.SessionId], item.SubmittedAt, item.IsLate, feedback.GetValueOrDefault(item.Id)));
            }

            var status = submittedLessons.Count == 0 ? "NotSubmitted"
                : submittedLessons.Count == lessons.Length && submittedLessons.All(lesson => lesson.BestScore >= assignment.MinimumScore)
                    ? "Completed" : "InProgress";
            return new AssignmentLearnerResult(member.UserId, member.DisplayName, member.Status, status, [.. submittedLessons]);
        }).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new(assignment.MinimumScore, lessons.Length, new(results, page, pageSize, count), assignment.IsArchived);
    }
}
