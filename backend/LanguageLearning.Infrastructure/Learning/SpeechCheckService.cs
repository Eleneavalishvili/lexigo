using System.Security.Cryptography;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Speech;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Learning.LearningQueries;

namespace LanguageLearning.Infrastructure.Learning;

public sealed record SpeechCheckFeedback(string Status, string Message, int HeartsRemaining, bool FreeRetryAvailable, AnswerFeedback? Answer = null);
public sealed record SpeechCheckOutcome(SpeechCheckFeedback? Value = null, bool Missing = false, string? Error = null, bool Unavailable = false, bool RateLimited = false);

public sealed class SpeechCheckService(LexiGoDbContext database, LearningAccess access, HeartBalanceService hearts,
    LearningAnswerService answers, ISpeechRecognizer recognizer, TimeProvider clock)
{
    public async Task<SpeechCheckOutcome> CheckAsync(Guid userId, Guid familyId, Guid sessionId, Guid exerciseId,
        Guid requestKey, PreparedAudio audio, CancellationToken cancellationToken)
    {
        if (requestKey == Guid.Empty || audio.Channels != 1 || audio.BitsPerSample != 16 || audio.SampleRate != 16000 || audio.DurationMilliseconds > 30000)
            return new(Error: "Use mono 16-bit WAV at 16000 Hz, up to 30 seconds, with a valid request key.");
        string hash = Convert.ToHexStringLower(SHA256.HashData(audio.Bytes.Span));
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var session = await database.LearningSessions.SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        var exercise = await database.SessionExercises.SingleOrDefaultAsync(item => item.Id == exerciseId && item.SessionId == sessionId && item.UserId == userId, cancellationToken);
        if (session is null || exercise is null) return new(Missing: true);
        var replay = await database.SpeechCheckAttempts.SingleOrDefaultAsync(item => item.UserId == userId && item.IdempotencyKey == requestKey, cancellationToken);
        if (replay is not null)
            return replay.SessionExerciseId == exerciseId && replay.AudioSha256 == hash
                ? new(Decode<SpeechCheckFeedback>(replay.ResponseSnapshot)) : new(Error: "This request key belongs to a different recording.");
        if (await database.AnswerAttempts.AnyAsync(item => item.UserId == userId && item.IdempotencyKey == requestKey, cancellationToken))
            return new(Error: "This request key was already used for another answer.");
        if (session.CompletedAt is not null) return new(Error: "This lesson is already complete.");
        var balance = await hearts.LockedAsync(userId, cancellationToken);
        session.Hearts = session.Mode == "Practice" ? 5 : balance.Count;
        if (session.Hearts == 0) return new(Error: "Your hearts are recovering. Wait for the refill or use free practice.");
        var current = await database.SessionExercises.Where(item => item.SessionId == sessionId
            && !database.AnswerAttempts.Any(answer => answer.UserId == userId && answer.SessionExerciseId == item.Id))
            .OrderBy(item => item.OrderNumber).Select(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (current != exerciseId || Decode<ExerciseView>(exercise.PublicSnapshot).Type != "SpeakingCheck")
            return new(Error: "Record an answer to the current speaking question.");
        if (await database.SpeechCheckAttempts.CountAsync(item => item.UserId == userId && item.CreatedAt > clock.GetUtcNow().AddMinutes(-10), cancellationToken) >= 30)
            return new(RateLimited: true);
        string language = await (from course in database.Courses
                                 join target in database.Languages on course.TargetLanguageId equals target.Id
                                 where course.Id == session.CourseId
                                 select target.Code).SingleAsync(cancellationToken);
        var recognition = await recognizer.RecognizeAsync(language, audio, cancellationToken);
        if (recognition.Status is SpeechRecognitionStatus.Disabled or SpeechRecognitionStatus.Unavailable or SpeechRecognitionStatus.Busy)
            return new(Unavailable: true);
        var evaluation = Decode<EvaluationSnapshot>(exercise.PrivateEvaluationSnapshot);
        bool matched = recognition.Status == SpeechRecognitionStatus.Recognized
            && evaluation.Answers.Any(answer => SpokenWordEvaluator.Matches(recognition.Text, answer));
        int earlierMisses = await database.SpeechCheckAttempts.CountAsync(item => item.UserId == userId && item.SessionExerciseId == exerciseId && item.Status == "Mismatch", cancellationToken);
        string status = matched ? "Matched" : recognition.Status == SpeechRecognitionStatus.NoSpeech || string.IsNullOrWhiteSpace(recognition.Text) ? "NoSpeech" : "Mismatch";
        int charged = 0;
        AnswerFeedback? accepted = null;
        if (matched)
        {
            var result = await answers.AnswerSpokenAsync(userId, familyId, sessionId,
                new(exerciseId, recognition.Text, [], requestKey), cancellationToken);
            if (result.Value is not AnswerFeedback feedback) return new(Error: result.Error ?? "The spoken answer could not be saved.");
            accepted = feedback;
        }
        else if (status == "Mismatch" && session.Mode != "Practice" && HeartRecovery.SpeakingMissCostsHeart(earlierMisses))
        {
            charged = hearts.Spend(balance) ? 1 : 0;
            session.Hearts = balance.Count;
        }
        int misses = earlierMisses + (status == "Mismatch" ? 1 : 0);
        bool freeRetry = session.Mode == "Practice" || balance.Unlimited || misses == 0;
        string message = status switch
        {
            "Matched" => "Those words matched. Nicely said!",
            "NoSpeech" => "We couldn’t recognize clear words. Try again; no heart was used.",
            _ when session.Mode == "Practice" => "Those words didn’t match. Try again; free practice does not use hearts.",
            _ when balance.Unlimited => "Those words didn’t match. Try again; Premium has unlimited hearts.",
            _ when charged == 0 => "Those words didn’t match. No heart was used this time; further unsuccessful checks cost one heart.",
            _ => "Those words didn’t match. One heart was used. Listen again before your next try."
        };
        var response = new SpeechCheckFeedback(status, message, session.Hearts, freeRetry, accepted);
        database.SpeechCheckAttempts.Add(new SpeechCheckAttempt
        {
            UserId = userId,
            SessionExerciseId = exerciseId,
            IdempotencyKey = requestKey,
            AudioSha256 = hash,
            Status = status,
            HeartsCharged = charged,
            ResponseSnapshot = Encode(response),
            CreatedAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(response);
    }
}
