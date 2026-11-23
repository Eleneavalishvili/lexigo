using System.Data;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Vocabulary;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Accounts.ExportJson;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class LearningDataExport(LexiGoDbContext database, VocabularyExport vocabulary,
    LearnerCommunicationExport communication, SimulatedBillingExport billing,
    AuthoredContentExport authoredContent, TimeProvider clock)
{
    public async Task WriteAsync(Guid userId, Stream destination, CancellationToken cancellationToken)
    {
        await using var snapshot = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var profile = await database.Users.AsNoTracking().Where(item => item.Id == userId)
            .Select(item => new { item.DisplayName, item.Email, item.EmailConfirmed, item.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        var preferences = await database.UserPreferences.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => new
            {
                item.TimeZone,
                item.DailyGoalXp,
                item.ActiveCourseId,
                item.Theme,
                item.ReduceMotion,
                item.SoundEnabled,
                item.LeaderboardOptIn,
                LeaderboardPhotoShared = item.SharedLeaderboardAvatarId != null,
                EmailRemindersEnabled = item.Reminder.Enabled,
                ReminderTime = item.Reminder.LocalTime,
                StudyWeekdays = item.Reminder.WeekDays
            }).SingleOrDefaultAsync(cancellationToken);
        var enrollments = database.Enrollments.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.CourseId)
            .Select(item => new { item.CourseId, item.EnrolledAt, item.PlacementLevel });
        var progress = database.LessonProgress.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.LessonId)
            .Select(item => new { item.LessonId, item.Status, item.BestScore, item.CompletedAt });
        var sessions = database.LearningSessions.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.StartedAt).ThenBy(item => item.Id)
            .Select(item => new
            {
                item.Id,
                item.CourseId,
                item.LessonId,
                item.Title,
                item.Mode,
                item.StartedAt,
                item.CompletedAt,
                item.Score,
                item.AwardedXp,
                item.RecommendedUnitId,
                item.PlacementAppliedAt
            });
        var xp = database.XpTransactions.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.EarnedAt).ThenBy(item => item.Id)
            .Select(item => new { item.CourseId, item.SourceType, item.Amount, item.EarnedAt });

        await StartAsync(destination, clock.GetUtcNow(), cancellationToken);
        await WriteValueAsync(destination, ",\"profile\":"u8.ToArray(), profile, cancellationToken);
        await WriteValueAsync(destination, ",\"preferences\":"u8.ToArray(), preferences, cancellationToken);
        await WriteSectionAsync(destination, ",\"enrollments\":"u8.ToArray(), enrollments, cancellationToken);
        await WriteSectionAsync(destination, ",\"lessonProgress\":"u8.ToArray(), progress, cancellationToken);
        await WriteSectionAsync(destination, ",\"learningSessions\":"u8.ToArray(), sessions, cancellationToken);
        await WriteSectionAsync(destination, ",\"xpHistory\":"u8.ToArray(), xp, cancellationToken);
        var cards = database.CompletionCards.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.IssuedAt).ThenBy(item => item.Id)
            .Select(item => new
            {
                item.Id,
                item.UnitId,
                item.CourseId,
                item.CourseTitle,
                item.UnitTitle,
                item.IssuedAt,
                item.CompletedAt,
                item.VerificationCode,
                item.LessonCount,
                item.GradedLessonCount,
                item.AverageBestScore,
                item.IsShared
            });
        await WriteSectionAsync(destination, ",\"completionCards\":"u8.ToArray(), cards, cancellationToken);
        await WriteActivityAsync(userId, destination, cancellationToken);
        await WriteAccountHistoryAsync(userId, destination, cancellationToken);
        await authoredContent.WriteSectionsAsync(userId, destination, cancellationToken);
        await communication.WriteSectionsAsync(userId, destination, cancellationToken);
        await billing.WriteSectionsAsync(userId, destination, cancellationToken);
        await vocabulary.WriteSectionsAsync(userId, destination, cancellationToken);
        await snapshot.CommitAsync(cancellationToken);
        await destination.WriteAsync("}"u8.ToArray(), cancellationToken);
    }

    private async Task WriteActivityAsync(Guid userId, Stream destination, CancellationToken cancellationToken)
    {
        var attempts = from attempt in database.AnswerAttempts.AsNoTracking()
                       join exercise in database.SessionExercises.AsNoTracking()
                           on new { attempt.UserId, Id = attempt.SessionExerciseId } equals new { exercise.UserId, exercise.Id }
                       where attempt.UserId == userId
                       orderby attempt.SubmittedAt, attempt.Id
                       select new
                       {
                           exercise.SessionId,
                           exercise.ExerciseId,
                           exercise.OrderNumber,
                           attempt.AnswerPayload,
                           attempt.IsGraded,
                           IsCorrect = attempt.IsGraded ? (bool?)attempt.IsCorrect : null,
                           attempt.SubmittedAt
                       };
        var activity = database.DailyActivities.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.LocalDate)
            .Select(item => new { item.LocalDate, item.EarnedXp, item.CompletedLessons, item.GoalReached });
        var achievements = from earned in database.UserAchievements.AsNoTracking()
                           join definition in database.Achievements on earned.AchievementId equals definition.Id
                           where earned.UserId == userId
                           orderby earned.EarnedAt, earned.AchievementId
                           select new { earned.AchievementId, definition.Code, definition.Title, earned.EarnedAt };
        var challenges = from entry in database.UserChallenges.AsNoTracking()
                         join definition in database.Challenges on entry.ChallengeId equals definition.Id
                         where entry.UserId == userId
                         orderby definition.StartsAt, entry.ChallengeId
                         select new
                         {
                             entry.ChallengeId,
                             definition.Title,
                             definition.StartsAt,
                             definition.EndsAt,
                             entry.Progress,
                             entry.CompletedAt,
                             entry.RewardedAt
                         };
        await WriteSectionAsync(destination, ",\"exerciseAttempts\":"u8.ToArray(), attempts, cancellationToken);
        await WriteSectionAsync(destination, ",\"dailyActivity\":"u8.ToArray(), activity, cancellationToken);
        await WriteSectionAsync(destination, ",\"achievements\":"u8.ToArray(), achievements, cancellationToken);
        await WriteSectionAsync(destination, ",\"challenges\":"u8.ToArray(), challenges, cancellationToken);
        var skills = from progress in database.SkillProgress.AsNoTracking()
                     join skill in database.Skills.AsNoTracking() on progress.SkillId equals skill.Id
                     where progress.UserId == userId
                     orderby progress.SkillId
                     select new { progress.SkillId, skill.CourseId, skill.Name, skill.Category,
                         progress.CorrectCount, progress.IncorrectCount, progress.LastPracticedAt };
        await WriteSectionAsync(destination, ",\"skillProgress\":"u8.ToArray(), skills, cancellationToken);
        var dialogues = database.DialogueSessions.AsNoTracking().Where(item => item.UserId == userId);
        await WriteSectionAsync(destination, ",\"dialogueSessions\":"u8.ToArray(),
            dialogues.OrderBy(item => item.StartedAt).ThenBy(item => item.Id)
                .Select(item => new { item.Id, item.DialogueId, item.CurrentNodeId, item.StartedAt, item.CompletedAt }),
            cancellationToken);
        var dialogueAttempts = from attempt in database.DialogueAttempts.AsNoTracking()
                               join session in dialogues on attempt.SessionId equals session.Id
                               orderby attempt.SubmittedAt, attempt.Id
                               select new { attempt.SessionId, attempt.ChoiceId, attempt.SubmittedAt };
        await WriteSectionAsync(destination, ",\"dialogueAttempts\":"u8.ToArray(), dialogueAttempts, cancellationToken);
    }

    private async Task WriteAccountHistoryAsync(Guid userId, Stream destination, CancellationToken cancellationToken)
    {
        var hearts = await database.LearnerHearts.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => new { item.Count, item.RefillAt }).SingleOrDefaultAsync(cancellationToken);
        var checks = from attempt in database.SpeechCheckAttempts.AsNoTracking()
                     join exercise in database.SessionExercises.AsNoTracking()
                         on new { attempt.UserId, Id = attempt.SessionExerciseId } equals new { exercise.UserId, exercise.Id }
                     where attempt.UserId == userId
                     orderby attempt.CreatedAt, attempt.Id
                     select new { exercise.SessionId, exercise.ExerciseId, attempt.Status, attempt.HeartsCharged, attempt.CreatedAt };
        var reminders = database.ReminderDeliveries.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.LocalDate).ThenBy(item => item.Type)
            .Select(item => new { item.LocalDate, item.Type, item.Status, item.SentAt });
        var sessions = database.RefreshSessions.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new { item.DeviceLabel, item.CreatedAt, item.ExpiresAt, item.RevokedAt, item.MfaVerified });

        await WriteValueAsync(destination, ",\"hearts\":"u8.ToArray(), hearts, cancellationToken);
        await WriteSectionAsync(destination, ",\"speechChecks\":"u8.ToArray(), checks, cancellationToken);
        await WriteSectionAsync(destination, ",\"reminderHistory\":"u8.ToArray(), reminders, cancellationToken);
        await WriteSectionAsync(destination, ",\"signInHistory\":"u8.ToArray(), sessions, cancellationToken);
    }
}
