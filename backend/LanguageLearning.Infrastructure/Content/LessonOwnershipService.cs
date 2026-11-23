using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed record TransferLessonOwner([Required, EmailAddress, MaxLength(256)] string Email,
    Guid ExpectedOwnerId, [Required, StringLength(500, MinimumLength = 3)] string Reason);

public sealed class LessonOwnershipService(LexiGoDbContext database, ContentTransactions content)
{
    public async Task<ContentOutcome> TransferAsync(Guid actorId, Guid familyId, Guid lessonId,
        TransferLessonOwner request, string traceId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 500
            || request.Reason.Any(char.IsControl))
            return new(Error: "Give a brief reason for handing over this private lesson.");
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        await AdministrationLocks.AcquireAsync(database, [actorId], token);
        var context = await content.LessonAsync(actorId, familyId, lessonId, token);
        if (context is not { } state || !state.Actor.IsAdmin) return new(Forbidden: true);
        if (state.Lesson.CurrentVersionId is not null || await database.LessonVersions.AnyAsync(
            item => item.LessonId == lessonId && (item.PublishedAt != null || item.Status == "Published"), token)
            || await database.LearningSessions.AnyAsync(item => item.LessonId == lessonId, token))
            return new(Error: "This handover is for never-published lessons without learner history.");
        string email = request.Email.Trim().ToUpperInvariant();
        var eligible = from user in database.Users
                       join membership in database.UserRoles on user.Id equals membership.UserId
                       join role in database.Roles on membership.RoleId equals role.Id
                       where user.NormalizedEmail == email && user.Status == "Active"
                           && user.EmailConfirmed && user.TwoFactorEnabled
                           && (role.Name == "Admin" || role.Name == "SuperAdmin"
                               || role.Name == "ContentAuthor" && database.UserLanguageScopes.Any(
                                   scope => scope.UserId == user.Id && scope.LanguageId == state.Course.TargetLanguageId))
                       select user.Id;
        Guid recipient = await eligible.Distinct().SingleOrDefaultAsync(token);
        if (recipient == Guid.Empty) return new(Error: "Choose an active author with MFA and access to this language.");
        if (state.Lesson.OwnerId == recipient) return new(lessonId);
        if (state.Lesson.OwnerId != request.ExpectedOwnerId)
            return new(Error: "The lesson owner changed. Refresh before handing it over.");
        Guid previousOwner = state.Lesson.OwnerId;
        state.Lesson.OwnerId = recipient;
        content.Audit(actorId, lessonId, "PrivateLessonOwnershipTransferred",
            $"Owner {previousOwner:N} transferred to {recipient:N}. {request.Reason.Trim()}", traceId);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(lessonId);
    }
}
