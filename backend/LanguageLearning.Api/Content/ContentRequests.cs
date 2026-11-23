using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Api.Content;

public sealed record UnitCreateRequest(Guid CourseId, [Required, StringLength(160)] string Title, [Required, StringLength(3000)] string Description,
    [Range(1, 10000)] int OrderNumber, [Required, StringLength(40)] string Difficulty);
public sealed record UnitEditRequest([Required, StringLength(160)] string Title,
    [Required, StringLength(3000)] string Description, [Range(1, 10000)] int OrderNumber,
    [Required, StringLength(40)] string Difficulty);
public sealed record LessonCreateRequest(Guid UnitId, [Required, StringLength(160)] string Title, [Required, StringLength(1000)] string Objective,
    [Required(AllowEmptyStrings = true), StringLength(20000)] string Explanation, [Range(1, 10000)] int OrderNumber);
public sealed record DraftUpdateRequest(Guid VersionId, [Required, StringLength(160)] string Title, [Required, StringLength(1000)] string Objective,
    [Required(AllowEmptyStrings = true), StringLength(20000)] string Explanation);
public sealed record ExerciseCreateRequest(Guid VersionId, [Required, StringLength(40)] string Type, [Required, StringLength(5000)] string Prompt,
    [Required, StringLength(5000)] string Explanation, [Range(1, 10000)] int OrderNumber, [Required, MaxLength(20)] string[] Options, [Required, MaxLength(20)] string[] Answers, bool IgnoreCase = true, bool IgnoreTerminalPunctuation = false, [MaxLength(20)] int[][]? TokenOrders = null, [MaxLength(10)] LanguageLearning.Infrastructure.Content.MatchingPairInput[]? MatchPairs = null, [StringLength(4000)] string? ContextText = null, Guid? AudioMediaId = null, int[]? SkillIds = null);
public sealed record VersionRequest(Guid VersionId);
public sealed record ArchiveVisibilityRequest([Required] bool? Archived, [Required, StringLength(500, MinimumLength = 3)] string Reason);
public sealed record MoveExerciseRequest(Guid VersionId, Guid NeighborId, bool Earlier);
public sealed record MovePositionRequest([Range(1, int.MaxValue)] int FromPosition, [Range(1, 10000)] int ToPosition);
public sealed record EditCourseRequest([Required, StringLength(120)] string Title,
    [Required, StringLength(1000)] string Description);
public sealed record ReviewRequest(Guid VersionId, [Required, RegularExpression("^(Approved|ChangesRequested)$")] string Decision, [Required, StringLength(3000, MinimumLength = 3)] string Comment);
public sealed record ArchiveRequest(Guid VersionId, [Required, StringLength(500, MinimumLength = 3)] string Reason);
public sealed record DuplicateLessonRequest(Guid VersionId, [Required, StringLength(160)] string Title);
public sealed record PreviewCheckRequest(Guid VersionId, Guid ExerciseId, [StringLength(2000)] string? Answer,
    [MaxLength(20)] Guid[]? OrderedTokenIds, [MaxLength(10)] LanguageLearning.Infrastructure.Learning.MatchedPair[]? MatchedPairs);
public sealed record PrerequisiteRequest([Required, MaxLength(50)] Guid[] LessonIds);
