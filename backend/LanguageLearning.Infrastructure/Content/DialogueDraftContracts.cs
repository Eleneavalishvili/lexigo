using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Infrastructure.Content;

public sealed record DialogueDraftInput(
    Guid EditToken,
    [Required, StringLength(160)] string Title,
    bool RequiresPremium,
    [Required, MinLength(2), MaxLength(40)] DialogueNodeInput[] Nodes);
public sealed record DialogueNodeInput(
    [Required, StringLength(80)] string Key,
    [Required, StringLength(4000)] string Text,
    bool IsStart, bool IsTerminal,
    [Required, MaxLength(6)] DialogueChoiceInput[] Choices, Guid? AudioMediaId = null);
public sealed record DialogueChoiceInput(
    [Required, StringLength(1000)] string Text,
    [Required, StringLength(4000)] string Feedback,
    bool IsCorrect, [StringLength(80)] string? NextNodeKey);
public sealed record CreateDialogueInput(Guid CourseId,
    [Required, RegularExpression("^[a-z0-9][a-z0-9-]{0,39}$")] string Scenario,
    [Required, StringLength(160)] string Title, bool RequiresPremium);
public sealed record DialogueTransition(Guid EditToken,
    [Required, StringLength(2000, MinimumLength = 3)] string Comment);
public sealed record StaffDialogueRow(Guid Id, Guid CourseId, string Title, string Scenario,
    int VersionNumber, string Status, bool RequiresPremium, Guid? OwnerId, Guid EditToken);
public sealed record StaffDialogue(StaffDialogueRow Dialogue, DialogueNodeInput[] Nodes,
    Guid? ReviewerId, DateTimeOffset? ReviewedAt, string? ReviewComment);
public sealed record StaffDialoguePage(StaffDialogueRow[] Items, int Page, int PageSize, int TotalCount);
