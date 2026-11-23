using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Learning.LearningQueries;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class LearningAnswerService(LexiGoDbContext database, LearningAccess access, TimeProvider clock, HeartBalanceService hearts)
{
    public Task<LearningOutcome> AnswerAsync(Guid userId, Guid familyId, Guid sessionId, AnswerInput input, CancellationToken cancellationToken) =>
        SaveAsync(userId, familyId, sessionId, input, false, cancellationToken);

    internal Task<LearningOutcome> AnswerSpokenAsync(Guid userId, Guid familyId, Guid sessionId, AnswerInput input, CancellationToken cancellationToken) =>
        SaveAsync(userId, familyId, sessionId, input, true, cancellationToken);

    private async Task<LearningOutcome> SaveAsync(Guid userId, Guid familyId, Guid sessionId, AnswerInput input, bool verifiedSpeech, CancellationToken cancellationToken)
    {
        Guid[] tokenOrder = input.OrderedTokenIds ?? [];
        MatchedPair[] matches = input.MatchedPairs ?? [];
        if (matches.Length > 10 || matches.Any(pair => pair is null) || tokenOrder.Length > 20 || input.IdempotencyKey == Guid.Empty || input.SelectedOptionIds is null || input.SelectedOptionIds.Length > 8 || input.AnswerText?.Length > 1000) return new(Error: "Enter a valid answer and request key.");
        await using var transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var session = await database.LearningSessions.SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null) return new(Missing: true);
        if (!verifiedSpeech && await database.SpeechCheckAttempts.AnyAsync(item => item.UserId == userId && item.IdempotencyKey == input.IdempotencyKey, cancellationToken))
            return new(Error: "This request key belongs to a voice check.");
        string payload = matches.Length > 0
            ? Encode(new
            {
                input.AnswerText,
                selectedOptionIds = input.SelectedOptionIds.Order().ToArray(),
                orderedTokenIds = tokenOrder,
                matchedPairs = matches.OrderBy(pair => pair.LeftOptionId).ThenBy(pair => pair.RightOptionId).ToArray()
            })
            : tokenOrder.Length == 0
            ? Encode(new { input.AnswerText, selectedOptionIds = input.SelectedOptionIds.Order().ToArray() })
            : Encode(new { input.AnswerText, selectedOptionIds = input.SelectedOptionIds.Order().ToArray(), orderedTokenIds = tokenOrder });
        if (input.PracticeCompleted)
            payload = Encode(new
            {
                practiceCompleted = true,
                input.AnswerText,
                selectedOptionIds = input.SelectedOptionIds.Order().ToArray(),
                orderedTokenIds = tokenOrder,
                matchedPairs = matches.OrderBy(pair => pair.LeftOptionId).ThenBy(pair => pair.RightOptionId).ToArray()
            });
        if (verifiedSpeech) payload = Encode(new { source = "LocalSpeech", input.AnswerText, selectedOptionIds = input.SelectedOptionIds });
        var replay = await database.AnswerAttempts.SingleOrDefaultAsync(item => item.UserId == userId && item.IdempotencyKey == input.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            // jsonb may change whitespace/property ordering; compare its parsed content.
            bool same = replay.SessionExerciseId == input.SessionExerciseId && System.Text.Json.Nodes.JsonNode.DeepEquals(
                System.Text.Json.Nodes.JsonNode.Parse(replay.AnswerPayload), System.Text.Json.Nodes.JsonNode.Parse(payload))
                && await database.SessionExercises.AnyAsync(item => item.Id == replay.SessionExerciseId && item.SessionId == sessionId, cancellationToken);
            return same ? new(Decode<AnswerFeedback>(replay.ResponseSnapshot)) : new(Error: "This request key was already used for a different answer.");
        }
        var balance = session.Mode == "Practice" ? null : await hearts.LockedAsync(userId, cancellationToken);
        if (balance is not null && session.CompletedAt is null) session.Hearts = balance.Count;
        if (session.CompletedAt is not null || session.Hearts == 0 && session.Mode != "Practice") return new(Error: "This session cannot accept another answer.");
        var exercises = await database.SessionExercises.Where(item => item.SessionId == sessionId).OrderBy(item => item.OrderNumber).ToArrayAsync(cancellationToken);
        var answered = await database.AnswerAttempts.Where(item => item.UserId == userId && exercises.Select(exercise => exercise.Id).ToArray().Contains(item.SessionExerciseId))
            .Select(item => item.SessionExerciseId).ToArrayAsync(cancellationToken);
        var current = exercises.FirstOrDefault(item => !answered.Contains(item.Id));
        if (current is null || current.Id != input.SessionExerciseId) return new(Error: "Answer the current exercise before moving on.");
        var view = Decode<ExerciseView>(current.PublicSnapshot);
        var evaluation = Decode<EvaluationSnapshot>(current.PrivateEvaluationSnapshot);
        if (verifiedSpeech != (view.Type == "SpeakingCheck"))
            return new(Error: "Speaking checks require an actual recording through the voice endpoint.");
        if (view.Type != "SentenceBuilder" && tokenOrder.Length != 0) return new(Error: "This exercise does not accept word tiles.");
        if (view.Type != "Matching" && matches.Length != 0) return new(Error: "This exercise does not accept matching pairs.");
        if (view.Type != "Speaking" && input.PracticeCompleted) return new(Error: "This exercise requires a graded answer, not a practice confirmation.");
        bool graded = view.Type != "Speaking";
        string feedbackText = evaluation.Feedback;
        bool correct;
        if (!graded)
        {
            if (!input.PracticeCompleted || !string.IsNullOrEmpty(input.AnswerText) || input.SelectedOptionIds.Length != 0 || tokenOrder.Length != 0 || matches.Length != 0)
                return new(Error: "Confirm your self-assessment without submitting a score or recording.");
            correct = false;
        }
        else if (ExerciseFormats.IsChoice(view.Type, view.Options.Length))
        {
            if (!string.IsNullOrEmpty(input.AnswerText) || input.SelectedOptionIds.Length != 1 || !view.Options.Any(item => item.Id == input.SelectedOptionIds[0])) return new(Error: "Choose one of the displayed answers.");
            correct = evaluation.CorrectOptionIds.Contains(input.SelectedOptionIds[0]);
        }
        else if (view.Type == "SpeakingCheck")
        {
            if (string.IsNullOrWhiteSpace(input.AnswerText) || input.SelectedOptionIds.Length != 0) return new(Error: "Submit a recognized spoken phrase.");
            correct = evaluation.Answers.Any(answer => SpokenWordEvaluator.Matches(input.AnswerText, answer));
            if (!correct) return new(Error: "Only a matching spoken phrase can complete this question.");
        }
        else if (view.Type is "Translation" or "FillBlank" or "Listening")
        {
            if (string.IsNullOrWhiteSpace(input.AnswerText) || input.SelectedOptionIds.Length != 0) return new(Error: "Type your answer.");
            correct = evaluation.Answers.Any(answer => TextAnswerEvaluator.Matches(input.AnswerText, answer, evaluation.IgnoreCase, evaluation.IgnoreTerminalPunctuation));
            if (!correct)
            {
                string? standardSpelling = evaluation.Answers.FirstOrDefault(answer =>
                    TextAnswerEvaluator.Matches(input.AnswerText, answer, evaluation.IgnoreCase,
                        evaluation.IgnoreTerminalPunctuation, allowUnaccentedVowels: true));
                if (standardSpelling is not null && await database.Courses.AnyAsync(
                    course => course.Id == session.CourseId && course.TargetLanguage.Code != "ru", cancellationToken))
                {
                    correct = true;
                    feedbackText = $"Standard spelling is “{standardSpelling}”. {evaluation.Feedback}".TrimEnd();
                }
            }
        }
        else if (view.Type == "SentenceBuilder")
        {
            if (!string.IsNullOrEmpty(input.AnswerText) || input.SelectedOptionIds.Length != 0 || !SentenceAnswerKeys.IsSelection(tokenOrder, view.Options.Select(item => item.Id).ToArray()))
                return new(Error: "Choose displayed word tiles without repeating a tile.");
            correct = SentenceAnswerKeys.Matches(tokenOrder, view.Options.ToDictionary(item => item.Id, item => item.Text), evaluation.AcceptedTokenOrders ?? []);
        }
        else if (view.Type == "Matching")
        {
            var left = view.Options.Where(item => item.Side == "left").Select(item => item.Id).ToArray();
            var right = view.Options.Where(item => item.Side == "right").Select(item => item.Id).ToArray();
            if (!string.IsNullOrEmpty(input.AnswerText) || input.SelectedOptionIds.Length != 0 || left.Length < 2
                || matches.Length != left.Length || matches.Length != right.Length
                || matches.Select(pair => pair.LeftOptionId).Distinct().Count() != matches.Length
                || matches.Select(pair => pair.RightOptionId).Distinct().Count() != matches.Length
                || matches.Any(pair => !left.Contains(pair.LeftOptionId) || !right.Contains(pair.RightOptionId)))
                return new(Error: "Connect every displayed item once, from left to right.");
            var expected = evaluation.MatchingPairs ?? [];
            correct = expected.Length == matches.Length && matches.All(pair => expected.Contains(pair));
        }
        else return new(Error: "This exercise format is not supported yet.");
        if (graded && !correct && balance is not null)
        {
            hearts.Spend(balance);
            session.Hearts = balance.Count;
        }
        var next = session.Hearts > 0 || session.Mode == "Practice" ? exercises.FirstOrDefault(item => item.OrderNumber > current.OrderNumber)?.Id : null;
        var feedback = new AnswerFeedback(correct, feedbackText, session.Hearts, next, graded,
            graded && !correct ? AnswerCorrection.Describe(view, evaluation) : null, view.Prompt);
        var submittedAt = clock.GetUtcNow();
        if (graded && session.PlacementMap is null)
            await ExerciseSkillTracking.RecordAsync(database, session, view.Type, evaluation.SkillIds,
                correct, submittedAt, cancellationToken);

        database.AnswerAttempts.Add(new AnswerAttempt
        {
            UserId = userId,
            SessionExerciseId = current.Id,
            IdempotencyKey = input.IdempotencyKey,
            AnswerPayload = payload,
            ResponseSnapshot = Encode(feedback),
            IsCorrect = correct,
            IsGraded = graded,
            SubmittedAt = submittedAt
        });
        await database.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new(feedback);
    }
}
