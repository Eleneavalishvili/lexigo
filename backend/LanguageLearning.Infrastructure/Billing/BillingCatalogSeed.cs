using LanguageLearning.Core.Billing;

namespace LanguageLearning.Infrastructure.Billing;

public static class BillingCatalogSeed
{
    public static readonly Guid Free = Guid.Parse("70000000-0000-0000-0000-000000000001");
    public static readonly Guid Premium = Guid.Parse("70000000-0000-0000-0000-000000000002");
    public static readonly Guid Monthly = Guid.Parse("71000000-0000-0000-0000-000000000001");
    public static readonly Guid Annual = Guid.Parse("71000000-0000-0000-0000-000000000002");
    public static readonly DateTimeOffset EffectiveFrom = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
    public static readonly Feature[] Features =
    [
        new() { Id = 1, Code = "CoreCourses", Description = "Core courses in all four languages" },
        new() { Id = 2, Code = "PersonalVocabulary", Description = "Your personal word library" },
        new() { Id = 3, Code = "BasicPractice", Description = "Core learning and free recovery practice" },
        new() { Id = 4, Code = "ProgressTracking", Description = "Saved learning progress" },
        new() { Id = 5, Code = "CoreListening", Description = "Listening inside core lessons" },
        new() { Id = 6, Code = "UnlimitedHearts", Description = "Learn without losing hearts" },
        new() { Id = 7, Code = "AdvancedAnalytics", Description = "Advanced learning analytics" },
        new() { Id = 8, Code = "ExtraDialogues", Description = "Extra situational dialogues" },
        new() { Id = 9, Code = "AdvancedPractice", Description = "Advanced personal practice" },
        new() { Id = 10, Code = "PremiumListeningPractice", Description = "Dedicated listening and speaking practice" },
        new() { Id = 11, Code = "CharacterConversations", Description = "Character conversation calls" }
    ];

    public static bool Available(string code) => code is "CoreCourses" or "PersonalVocabulary" or "BasicPractice"
        or "ProgressTracking" or "CoreListening" or "UnlimitedHearts" or "AdvancedAnalytics" or "ExtraDialogues"
        or "AdvancedPractice" or "PremiumListeningPractice";
}
