namespace LanguageLearning.Infrastructure.Content;

internal static partial class StarterDialogues
{
    internal sealed record Branch(string Response, string FollowUp, string Reply, string Ending);
    internal sealed record Scenario(string Key, string Title, string Opening, Branch First, Branch Second,
        string Feedback, bool RequiresPremium = false);

    internal static Scenario[] ForLanguage(string code) => code switch
    {
        "es" => Es,
        "it" => It,
        "de" => De,
        "ru" => Ru,
        _ => []
    };
}
