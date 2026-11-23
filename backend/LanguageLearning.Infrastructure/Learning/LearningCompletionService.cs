using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class LearningCompletionService(LexiGoDbContext database, LearningAccess access, LearningQueries queries,
    TimeProvider clock, HeartBalanceService hearts, AchievementAwards achievements, ChallengeProgression challenges,
    CompletionCardIssuer cards)
{
    public async Task<LearningOutcome> CompleteAsync(Guid userId, Guid familyId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var session = await database.LearningSessions.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (session is null) return new(Missing: true);
        if (session.CompletedAt is not null) return new(await queries.SessionAsync(userId, id, cancellationToken));
        if (session.Mode != "Practice") session.Hearts = (await hearts.LockedAsync(userId, cancellationToken)).Count;
        var exercises = await database.SessionExercises.Where(item => item.SessionId == id).ToArrayAsync(cancellationToken);
        var exerciseIds = exercises.Select(item => item.Id).ToArray();
        var attempts = await database.AnswerAttempts.Where(item => item.UserId == userId && exerciseIds.Contains(item.SessionExerciseId)).ToArrayAsync(cancellationToken);
        if (attempts.Length != exerciseIds.Length && (session.Hearts > 0 || session.Mode == "Practice")) return new(Error: "Finish every exercise before completing this session.");
        var assessment = SessionAssessment.Calculate(exercises, attempts, session.PassScore);
        session.Score = assessment.Score;
        session.CompletedAt = clock.GetUtcNow();
        bool passed = assessment.MeetsCompletion;
        if (session.Mode == "Lesson" && session.LessonId is { } lessonId)
        {
            var progress = await database.LessonProgress.SingleAsync(item => item.UserId == userId && item.LessonId == lessonId, cancellationToken);
            bool firstPass = passed && assessment.GradedCount > 0 && !await database.XpTransactions.AnyAsync(item => item.UserId == userId
                && item.SourceType == "FirstLessonCompletion" && item.SourceId == lessonId, cancellationToken);
            progress.BestScore = Math.Max(progress.BestScore, session.Score);
            if (passed) { progress.Status = "Completed"; progress.CompletedAt ??= session.CompletedAt; }
            if (firstPass)
            {
                session.AwardedXp = session.FirstCompletionXp;
                database.XpTransactions.Add(new XpTransaction
                {
                    UserId = userId,
                    CourseId = session.CourseId,
                    SourceType = "FirstLessonCompletion",
                    SourceId = lessonId,
                    Amount = session.AwardedXp,
                    EarnedAt = session.CompletedAt.Value
                });
                var preference = await database.UserPreferences.SingleAsync(item => item.UserId == userId, cancellationToken);
                var zone = TimeZoneInfo.FindSystemTimeZoneById(preference.TimeZone);
                DateOnly date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(session.CompletedAt.Value, zone).DateTime);
                var activity = await database.DailyActivities.SingleOrDefaultAsync(item => item.UserId == userId && item.LocalDate == date, cancellationToken);
                if (activity is null) { activity = new DailyActivity { UserId = userId, LocalDate = date }; database.DailyActivities.Add(activity); }
                activity.EarnedXp += session.AwardedXp;
                activity.CompletedLessons++;
                activity.GoalReached |= activity.EarnedXp >= preference.DailyGoalXp;
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        if (session.AwardedXp > 0)
            await challenges.CompleteLessonLockedAsync(userId, session.CourseId, session.CompletedAt.Value, cancellationToken);
        if (session.Mode == "Lesson" && passed) await achievements.AwardLockedAsync(userId, cancellationToken);
        if (session.Mode == "Lesson" && passed && session.LessonId is { } completedLesson)
            await cards.IssueForLessonLockedAsync(userId, completedLesson, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(await queries.SessionAsync(userId, id, cancellationToken));
    }
}
