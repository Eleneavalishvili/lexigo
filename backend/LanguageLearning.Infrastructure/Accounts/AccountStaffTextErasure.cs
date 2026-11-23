using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

internal static class AccountStaffTextErasure
{
    internal const string RemovedNote = "Private note erased after account deletion.";

    // Called only inside the authenticated deletion transaction, with the account locked.
    // Decisions, dates and published authorship remain; free-text notes do not.
    internal static async Task EraseAsync(LexiGoDbContext database, Guid userId, CancellationToken token)
    {
        await database.ContentReviews.Where(item => item.ReviewerId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Comment, RemovedNote), token);
        await database.VocabularyRevisions.Where(item => item.ReviewerId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ReviewComment, (string?)null), token);
        await database.ContentReports.Where(item => item.ResolvedBy == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Resolution, RemovedNote), token);
        await database.TeacherFeedback.Where(item => item.TeacherId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Message, RemovedNote), token);

        var classes = database.Classrooms.Where(item => item.TeacherId == userId);
        var assignments = database.Assignments.Where(item => classes.Any(classroom => classroom.Id == item.ClassroomId));
        await database.Notifications.Where(item => item.AssignmentId != null
            && assignments.Any(assignment => assignment.Id == item.AssignmentId))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Body, "Archived assignment"), token);
        await assignments
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Title, "Archived assignment"), token);
        await classes.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.Name, "Archived class")
            .SetProperty(item => item.Description, ""), token);

        string accountId = userId.ToString();
        await database.AuditLogs.Where(item => item.ActorId == userId
            || item.EntityType == "User" && item.EntityId == accountId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Reason, RemovedNote), token);
    }
}
