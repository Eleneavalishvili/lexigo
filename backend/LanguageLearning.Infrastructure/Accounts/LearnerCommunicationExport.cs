using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Accounts.ExportJson;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class LearnerCommunicationExport(LexiGoDbContext database)
{
    internal async Task WriteSectionsAsync(Guid userId, Stream destination, CancellationToken cancellationToken)
    {
        var memberships = from membership in database.ClassMemberships.AsNoTracking()
                          join classroom in database.Classrooms on membership.ClassroomId equals classroom.Id
                          where membership.UserId == userId
                          orderby membership.JoinedAt, membership.ClassroomId
                          select new { membership.ClassroomId, classroom.CourseId, classroom.Name, membership.JoinedAt, membership.Status };
        var submissions = database.AssignmentSubmissions.AsNoTracking().Where(item => item.UserId == userId);
        var assignments = from submission in submissions
                          join assignment in database.Assignments on submission.AssignmentId equals assignment.Id
                          orderby submission.SubmittedAt, submission.Id
                          select new
                          {
                              submission.Id, submission.AssignmentId, assignment.ClassroomId, assignment.Title,
                              submission.SessionId, submission.SubmittedAt, submission.IsLate
                          };
        var feedback = from item in database.TeacherFeedback.AsNoTracking()
                       join submission in submissions on item.SubmissionId equals submission.Id
                       orderby item.CreatedAt, item.Id
                       select new { item.Id, item.SubmissionId, item.Message, item.CreatedAt };
        var extraStudy = from request in database.ExtraStudyRequests.AsNoTracking()
                         join offer in database.ExtraStudyOffers on request.OfferId equals offer.Id
                         where request.LearnerId == userId
                         orderby request.CreatedAt, request.Id
                         select new { request.Id, offer.CourseId, request.Status, request.CreatedAt };
        var notifications = database.Notifications.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.AssignmentId, item.Type, item.Title, item.Body, item.ReadAt, item.CreatedAt });
        var reports = database.ContentReports.AsNoTracking().Where(item => item.ReporterId == userId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new
            {
                item.Id, item.LanguageId, item.ResourceType,
                ResourceId = item.ResourceType == "RankingName" ? Guid.Empty : item.ResourceId,
                item.VersionId,
                item.Reason, item.Comment, item.Status, item.Resolution, item.CreatedAt
            });

        await WriteSectionAsync(destination, ",\"classMemberships\":"u8.ToArray(), memberships, cancellationToken);
        await WriteSectionAsync(destination, ",\"assignmentSubmissions\":"u8.ToArray(), assignments, cancellationToken);
        await WriteSectionAsync(destination, ",\"assignmentFeedback\":"u8.ToArray(), feedback, cancellationToken);
        await WriteSectionAsync(destination, ",\"extraStudyRequests\":"u8.ToArray(), extraStudy, cancellationToken);
        await WriteSectionAsync(destination, ",\"notifications\":"u8.ToArray(), notifications, cancellationToken);
        await WriteSectionAsync(destination, ",\"contentReports\":"u8.ToArray(), reports, cancellationToken);
    }
}
