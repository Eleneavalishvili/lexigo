namespace LanguageLearning.Infrastructure.Learning;

public sealed record LearningOutcome(object? Value = null, string? Error = null, bool Missing = false,
    bool Unavailable = false, bool PremiumRequired = false, bool Created = false);
public sealed record LearningOption(Guid Id, string Text, string? Side = null, AudioInformation? Audio = null);
public sealed record MatchedPair(Guid LeftOptionId, Guid RightOptionId);
public sealed record AudioInformation(string Source, string License, int DurationMilliseconds);
public sealed record AudioPlaybackLink(string Url, int ExpiresInSeconds);
public sealed record ExerciseView(Guid Id, string Type, string Prompt, LearningOption[] Options, string? ContextText = null, AudioInformation? Audio = null);
public sealed record EvaluationSnapshot(string[] Answers, Guid[] CorrectOptionIds, string Feedback,
    bool IgnoreCase, bool IgnoreTerminalPunctuation, Guid[][]? AcceptedTokenOrders = null,
    MatchedPair[]? MatchingPairs = null, int[]? SkillIds = null);
public sealed record AnswerInput(Guid SessionExerciseId, string? AnswerText, Guid[] SelectedOptionIds, Guid IdempotencyKey, Guid[]? OrderedTokenIds = null, MatchedPair[]? MatchedPairs = null, bool PracticeCompleted = false);
public sealed record AnswerFeedback(bool IsCorrect, string Feedback, int HeartsRemaining, Guid? NextExerciseId, bool IsGraded = true, string? CorrectAnswer = null, string? Prompt = null);
public sealed record ExerciseResult(string Prompt, bool IsCorrect, string Feedback, string? ContextText = null, Guid? Id = null, AudioInformation? Audio = null, bool IsGraded = true, LearningOption[]? Options = null, string? CorrectAnswer = null);
public sealed record SessionFeedback(Guid ExerciseId, AnswerFeedback Feedback);
public sealed record SessionView(Guid Id, Guid CourseId, Guid? LessonId, Guid VersionId, string Title, string Explanation,
    string Mode, int Hearts, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int Score, int PassScore, bool Passed, int AwardedXp,
    int AnsweredCount, int CorrectCount, int TotalCount, int Accuracy, ExerciseView? CurrentExercise, SessionFeedback? LastFeedback, ExerciseResult[] Results, int GradedExerciseCount = 0, int GradedAnsweredCount = 0, int SelfAssessmentCount = 0, int SpeakingMissCount = 0, bool UnlimitedHearts = false, bool IsPlacement = false, ExerciseView? AnsweredExercise = null, int? PracticeSkillId = null);
public sealed record LessonMapItem(Guid Id, Guid UnitId, string Title, string Objective, int OrderNumber, string Status,
    int BestScore, bool IsUnlocked, Guid[] MissingPrerequisites, Guid? ActiveSessionId, bool HasGradedScore = true);
public sealed record LearningUnit(Guid Id, string Title, string Description, int OrderNumber);
public sealed record OpenLearningSession(Guid Id, Guid? LessonId, string Title, string Mode);
public sealed record CourseProgress(Guid CourseId, string Title, bool IsEnrolled, LearningUnit[] Units, LessonMapItem[] Lessons, OpenLearningSession[] OpenSessions, string? StartingLevel = null, Guid? RecommendedUnitId = null);
public sealed record EnrollmentView(Guid CourseId, string Title, bool IsPublished, DateTimeOffset EnrolledAt, string? PlacementLevel);
public sealed record EnrollmentReceipt(Guid CourseId, DateTimeOffset EnrolledAt);
public sealed record EnrollmentPage(EnrollmentView[] Items, int Page, int PageSize, int TotalCount);
