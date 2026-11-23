using LanguageLearning.Core.Learning;
using static LanguageLearning.Infrastructure.Learning.LearningQueries;

namespace LanguageLearning.Infrastructure.Learning;

internal sealed record SessionAssessment(int GradedCount, int GradedAnsweredCount, int CorrectCount,
    int SelfAssessmentCount, int Score, int Accuracy, bool MeetsCompletion)
{
    public static SessionAssessment Calculate(IReadOnlyCollection<SessionExercise> exercises, IReadOnlyCollection<AnswerAttempt> attempts, int passScore)
    {
        var gradedIds = exercises.Where(item => Decode<ExerciseView>(item.PublicSnapshot).Type != "Speaking").Select(item => item.Id).ToHashSet();
        var gradedAttempts = attempts.Where(item => item.IsGraded && gradedIds.Contains(item.SessionExerciseId)).ToArray();
        int correct = gradedAttempts.Count(item => item.IsCorrect);
        int score = gradedIds.Count == 0 ? 0 : correct * 100 / gradedIds.Count;
        return new(gradedIds.Count, gradedAttempts.Length, correct, attempts.Count(item => !gradedIds.Contains(item.SessionExerciseId)),
            score, gradedAttempts.Length == 0 ? 0 : correct * 100 / gradedAttempts.Length,
            exercises.Count > 0 && attempts.Count == exercises.Count && (gradedIds.Count == 0 || score >= passScore));
    }
}
