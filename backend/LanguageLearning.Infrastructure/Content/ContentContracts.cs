using System.ComponentModel.DataAnnotations;
namespace LanguageLearning.Infrastructure.Content;

public sealed record UnitInput(string Title, string Description, int OrderNumber, string Difficulty);
public sealed record LessonInput(Guid UnitId, string Title, string Objective, string Explanation, int OrderNumber);
public sealed record DraftInput(Guid VersionId, string Title, string Objective, string Explanation);
public sealed record MatchingPairInput([Required, StringLength(300)] string Left, [Required, StringLength(300)] string Right, Guid? LeftAudioMediaId = null);
public sealed record ExerciseInput(string Type, string Prompt, string Explanation, int OrderNumber, string[] Options, string[] Answers, bool IgnoreCase = true, bool IgnoreTerminalPunctuation = false, int[][]? TokenOrders = null, MatchingPairInput[]? MatchPairs = null, string? ContextText = null, Guid? AudioMediaId = null, int[]? SkillIds = null);
public sealed record StaffExercise(Guid Id, string Type, string Prompt, string Explanation, int OrderNumber, Guid? AudioMediaId,
    string EvaluationRules, IReadOnlyList<StaffOption> Options, IReadOnlyList<StaffAnswer> Answers, string? ContextText = null, int[]? SkillIds = null);
public sealed record StaffSkill(int Id, string Name, bool IsArchived = false);
public sealed record StaffOption(Guid Id, string Text, int OrderNumber, string? MatchKey, Guid? AudioMediaId = null);
public sealed record StaffAnswer(Guid Id, string AnswerText, string? OrderedTokenIds);
public sealed record StaffLesson(Guid Id, Guid UnitId, Guid OwnerId, int LanguageId, string Status, Guid? CurrentVersionId,
    Guid VersionId, int VersionNumber, string VersionStatus, string Title, string Objective, string Explanation, Guid VersionCreatedBy,
    IReadOnlyList<StaffExercise> Exercises, IReadOnlyList<ReviewNote> Reviews,
    DateTimeOffset? VersionPublishedAt = null, DateTimeOffset? PrivateTextErasedAt = null, bool IsLatestVersion = true, StaffSkill[]? Skills = null,
    int OrderNumber = 1);
public sealed record ReviewNote(Guid ReviewerId, string Decision, string Comment, DateTimeOffset ReviewedAt);
public sealed record ContentOutcome(Guid? Id = null, string? Error = null, bool Forbidden = false);
