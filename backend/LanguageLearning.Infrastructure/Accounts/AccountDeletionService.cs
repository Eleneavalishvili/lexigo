using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Billing;
using LanguageLearning.Core.Administration;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class AccountDeletionService(LexiGoDbContext database, UserManager<ApplicationUser> users,
    MfaCodeVerifier codes, TimeProvider clock, AccountDeletionResponsibilities responsibilities)
{
    public async Task<string?> DeleteAsync(Guid userId, Guid familyId, string password, string? code,
        string? recoveryCode, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AdministrationLocks.AcquireAsync(database, [userId], cancellationToken);
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != "Active" || await users.IsLockedOutAsync(user))
            return "Your account could not be verified.";
        bool session = await database.RefreshSessions.AnyAsync(item => item.UserId == userId && item.FamilyId == familyId
            && item.RevokedAt == null && item.ExpiresAt > clock.GetUtcNow() && item.SecurityStamp == user.SecurityStamp,
            cancellationToken);
        if (!session) return "Sign in again before deleting your account.";
        bool verified = await users.CheckPasswordAsync(user, password);
        if (verified && user.TwoFactorEnabled)
            verified = !string.IsNullOrWhiteSpace(recoveryCode)
                ? (await users.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode.Trim())).Succeeded
                : !string.IsNullOrWhiteSpace(code) && await codes.VerifyAsync(user, code);
        if (!verified)
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync(cancellationToken);
            return "Check your password and authentication code.";
        }
        var roles = await users.GetRolesAsync(user);
        if (roles.Contains(RoleNames.SuperAdmin))
        {
            int remaining = await (from link in database.UserRoles
                                   join role in database.Roles on link.RoleId equals role.Id
                                   join account in database.Users on link.UserId equals account.Id
                                   where role.Name == RoleNames.SuperAdmin && account.Status == "Active"
                                       && account.EmailConfirmed && account.TwoFactorEnabled
                                   select account.Id).CountAsync(cancellationToken);
            if (remaining <= 1) return "The last active Super Admin must appoint a replacement first.";
        }
        string? responsibility = (await responsibilities.InspectAsync(userId, cancellationToken)).FirstOrDefault()?.Message;
        if (responsibility is not null) return responsibility;
        await AccountStaffTextErasure.EraseAsync(database, userId, cancellationToken);
        await ErasePrivateDataAsync(userId, cancellationToken);
        await database.BillingPreferences.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.Subscriptions.Where(item => item.UserId == userId && item.Status == "Active")
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Expired")
                .SetProperty(item => item.CancelAtPeriodEnd, true).SetProperty(item => item.ScheduledPlanPriceId, (Guid?)null),
                cancellationToken);
        await database.TestCards.Where(item => item.UserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Nickname, "Deleted account")
                .SetProperty(item => item.Status, "Blocked").SetProperty(item => item.Version, item => item.Version + 1),
                cancellationToken);
        await database.Enrollments.Where(item => item.UserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.PlacementLevel, (string?)null), cancellationToken);
        await database.UserRoles.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.UserClaims.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.UserLogins.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.UserTokens.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.UserLanguageScopes.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        user.Status = "Deleted";
        user.DisplayName = "Deleted account";
        user.UserName = $"deleted-{user.Id:N}";
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        user.Email = null;
        user.NormalizedEmail = null;
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.AvatarMediaId = null;
        await database.MediaAssets.Where(item => item.UploadedBy == userId
            && item.MimeType == "image/png" && item.Source == ProfileAvatarService.MediaSource)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.StorageState, "Deleting"), cancellationToken);
        user.PasswordHash = null;
        user.TeacherAccessCodeHash = null;
        user.TwoFactorEnabled = false;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = null,
            EntityType = "User",
            EntityId = user.Id.ToString(),
            Action = "AccountDeleted",
            Reason = "Account owner confirmed private data erasure.",
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        var refunds = database.Refunds.Where(item => item.CreatedBy == userId || database.PaymentAttempts
            .Any(payment => payment.Id == item.PaymentId && payment.UserId == userId));
        await database.AuditLogs.Where(item => item.EntityType == "Refund"
            && refunds.Any(refund => refund.Id.ToString() == item.EntityId))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Reason, Refund.ErasedReason), cancellationToken);
        await refunds.ExecuteUpdateAsync(update => update.SetProperty(item => item.Reason, Refund.ErasedReason),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    private async Task ErasePrivateDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        await database.BillingOperations.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        var cards = database.CompletionCards.Where(item => item.UserId == userId);
        // Keep report outcomes without retaining a deleted person's public name or complaint text.
        await database.ContentReports.Where(item => item.Reason == "PublicName"
            && (item.ResourceType == "RankingName" && item.ResourceId == userId
                || item.ResourceType == "CompletionCard" && cards.Any(card => card.Id == item.ResourceId)))
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.ResourceId, Guid.Empty)
                .SetProperty(item => item.Comment, "This report concerns a deleted account.")
                .SetProperty(item => item.Resolution, "The account was deleted and identifying report text was erased.")
                .SetProperty(item => item.Status, item => item.Status == "New" || item.Status == "InReview"
                    ? "Rejected" : item.Status), cancellationToken);
        // The report update waits for any in-flight moderator decision before its audit is redacted.
        await database.AuditLogs.Where(item => item.EntityType == "CompletionCard"
            && cards.Any(card => card.Id.ToString() == item.EntityId))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Reason, AccountStaffTextErasure.RemovedNote),
                cancellationToken);
        await database.CompletionCards.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.SkillProgress.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.DialogueSessions.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        var exercises = database.SessionExercises.Where(item => item.UserId == userId);
        var submissions = database.AssignmentSubmissions.Where(item => item.UserId == userId);
        var saved = database.SavedWords.Where(item => item.UserId == userId);
        await database.VocabularyRevisions.Where(item => item.AuthorId == userId && item.Status == "Withdrawn")
            .ExecuteDeleteAsync(cancellationToken);
        await database.Notifications.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.TeacherFeedback.Where(item => submissions.Any(submission => submission.Id == item.SubmissionId))
            .ExecuteDeleteAsync(cancellationToken);
        await submissions.ExecuteDeleteAsync(cancellationToken);
        await database.SpeechCheckAttempts.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.AnswerAttempts.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.SessionOptionAudio.Where(item => exercises.Any(exercise => exercise.Id == item.SessionExerciseId))
            .ExecuteDeleteAsync(cancellationToken);
        await exercises.ExecuteDeleteAsync(cancellationToken);
        await database.LearningSessions.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.LessonProgress.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.CollectionWords.Where(item => saved.Any(word => word.Id == item.SavedWordId)).ExecuteDeleteAsync(cancellationToken);
        await database.WordReviews.Where(item => saved.Any(word => word.Id == item.SavedWordId)).ExecuteDeleteAsync(cancellationToken);
        await saved.ExecuteDeleteAsync(cancellationToken);
        await database.WordCollections.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.ExtraStudyLessons.Where(item => database.ExtraStudyRequests.Any(request =>
            request.Id == item.RequestId && request.LearnerId == userId)).ExecuteDeleteAsync(cancellationToken);
        await database.ExtraStudyRequests.Where(item => item.LearnerId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.ClassMemberships.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.ContentReports.Where(item => item.ReporterId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.ImportJobs.Where(item => item.AuthorId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.UserAchievements.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.UserChallenges.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.LearnerHearts.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.ReminderDeliveries.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.EmailOutboxMessages.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.UserPreferences.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.RefreshSessions.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
