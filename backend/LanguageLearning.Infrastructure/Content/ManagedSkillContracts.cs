using System.ComponentModel.DataAnnotations;

namespace LanguageLearning.Infrastructure.Content;

public sealed record ManagedSkill(int Id, Guid CourseId, string Name, string Category, bool IsArchived, int Revision);
public sealed record ManagedSkillPage(ManagedSkill[] Items, int Page, int PageSize, int TotalCount);
public sealed record SkillInput(
    Guid CourseId,
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(40)] string Category);
public sealed record SkillUpdate(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(40)] string Category,
    [Range(1, int.MaxValue)] int Revision,
    [Required, MinLength(3), MaxLength(500)] string Reason);
public sealed record SkillVisibility(
    bool Archived,
    [Range(1, int.MaxValue)] int Revision,
    [Required, MinLength(3), MaxLength(500)] string Reason);
public sealed record SkillArchiveRequest(
    [Range(1, int.MaxValue)] int Revision,
    [Required, MinLength(3), MaxLength(500)] string Reason);
public sealed record SkillWriteResult(ManagedSkill? Value = null, bool Forbidden = false,
    bool Missing = false, string? Error = null);
