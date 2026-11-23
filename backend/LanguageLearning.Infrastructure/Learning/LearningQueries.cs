using System.Text.Json;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class LearningQueries(LexiGoDbContext database, HeartBalanceService hearts)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    internal static T Decode<T>(string value) => JsonSerializer.Deserialize<T>(value, Json)!;

    public async Task<SessionView?> SessionAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var session = await database.LearningSessions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (session is null) return null;
        bool unlimited = false;
        if (session.CompletedAt is null && session.Mode != "Practice")
        {
            var balance = await hearts.StatusAsync(userId, cancellationToken);
            session.Hearts = balance.Count;
            unlimited = balance.Unlimited;
        }
        var exercises = await database.SessionExercises.AsNoTracking().Where(item => item.SessionId == id).OrderBy(item => item.OrderNumber).ToArrayAsync(cancellationToken);
        var ids = exercises.Select(item => item.Id).ToArray();
        var attempts = await database.AnswerAttempts.AsNoTracking().Where(item => item.UserId == userId && ids.Contains(item.SessionExerciseId)).ToArrayAsync(cancellationToken);
        var assessment = SessionAssessment.Calculate(exercises, attempts, session.PassScore);
        var current = exercises.FirstOrDefault(item => !attempts.Any(answer => answer.SessionExerciseId == item.Id));
        int speechMisses = current is not null && Decode<ExerciseView>(current.PublicSnapshot).Type == "SpeakingCheck"
            ? await database.SpeechCheckAttempts.CountAsync(item => item.UserId == userId && item.SessionExerciseId == current.Id && item.Status == "Mismatch", cancellationToken) : 0;
        var last = attempts.OrderByDescending(item => item.SubmittedAt).ThenByDescending(item => exercises.Single(exercise => exercise.Id == item.SessionExerciseId).OrderNumber).FirstOrDefault();
        return new(session.Id, session.CourseId, session.LessonId, session.LessonVersionId, session.Title, session.Explanation,
            session.Mode, session.Hearts, session.StartedAt, session.CompletedAt, session.Score, session.PassScore, session.CompletedAt is not null && assessment.MeetsCompletion, session.AwardedXp, attempts.Length,
            assessment.CorrectCount, exercises.Length, assessment.Accuracy,
            session.CompletedAt == null && (session.Hearts > 0 || session.Mode == "Practice") && current is not null ? Decode<ExerciseView>(current.PublicSnapshot) : null,
            last is null ? null : new(last.SessionExerciseId, RevealedFeedback(last, exercises.Single(item => item.Id == last.SessionExerciseId))),
            session.CompletedAt is null ? [] : exercises.Where(item => attempts.Any(attempt => attempt.SessionExerciseId == item.Id)).Select(item =>
            {
                var attempt = attempts.Single(answer => answer.SessionExerciseId == item.Id);
                var view = Decode<ExerciseView>(item.PublicSnapshot);
                return new ExerciseResult(view.Prompt, attempt.IsCorrect, Decode<AnswerFeedback>(attempt.ResponseSnapshot).Feedback,
                    view.ContextText, item.Id, view.Audio, attempt.IsGraded, view.Type == "Matching" ? view.Options : null, RevealedFeedback(attempt, item).CorrectAnswer);
            }).ToArray(), assessment.GradedCount, assessment.GradedAnsweredCount, assessment.SelfAssessmentCount, speechMisses, unlimited, session.PlacementMap != null,
            last is null ? null : Decode<ExerciseView>(exercises.Single(item => item.Id == last.SessionExerciseId).PublicSnapshot), session.PracticeSkillId);
    }
    private static AnswerFeedback RevealedFeedback(LanguageLearning.Core.Learning.AnswerAttempt attempt,
        LanguageLearning.Core.Learning.SessionExercise exercise)
    {
        var feedback = Decode<AnswerFeedback>(attempt.ResponseSnapshot);
        return feedback.IsGraded && !feedback.IsCorrect && feedback.CorrectAnswer is null
            ? feedback with
            {
                CorrectAnswer = AnswerCorrection.Describe(Decode<ExerciseView>(exercise.PublicSnapshot),
                Decode<EvaluationSnapshot>(exercise.PrivateEvaluationSnapshot))
            } : feedback;
    }

}
