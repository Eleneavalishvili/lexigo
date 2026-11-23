namespace LanguageLearning.Core.Learning;

public sealed class SkillProgress
{
    public Guid UserId { get; set; }
    public int SkillId { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public DateTimeOffset? LastPracticedAt { get; set; }
}
