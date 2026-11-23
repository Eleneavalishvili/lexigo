namespace LanguageLearning.Core.Courses;

public sealed class Language
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string EnglishName { get; set; }
    public required string NativeName { get; set; }
    public string WritingDirection { get; set; } = "ltr";
}
