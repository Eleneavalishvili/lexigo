namespace LanguageLearning.Core.Accounts;

public static class RoleNames
{
    public const string Learner = "Learner";
    public const string Teacher = "Teacher";
    public const string ContentAuthor = "ContentAuthor";
    public const string Reviewer = "Reviewer";
    public const string Moderator = "Moderator";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";

    public static IReadOnlyList<string> All { get; } =
        [Learner, Teacher, ContentAuthor, Reviewer, Moderator, Admin, SuperAdmin];
}
