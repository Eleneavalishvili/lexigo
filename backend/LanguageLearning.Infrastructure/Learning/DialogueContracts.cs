namespace LanguageLearning.Infrastructure.Learning;

public sealed record DialogueListing(Guid Id, string Title, string Scenario, int VersionNumber,
    Guid? ResumeSessionId, bool HasCompleted, bool RequiresPremium = false, bool Locked = false);
public sealed record DialogueOption(Guid Id, string Text);
public sealed record DialogueTurn(Guid Id, string Text, bool IsTerminal, DialogueOption[] Choices);
public sealed record DialogueStep(string Prompt, string Response, string Feedback, bool IsCorrect);
public sealed record DialogueReviewPhrase(string Text, string Tip);
public sealed record DialogueView(Guid Id, Guid DialogueId, string Title, DialogueTurn Current,
    DateTimeOffset? CompletedAt, DialogueStep[] Path, DialogueReviewPhrase[] ReviewPhrases, string LanguageCode,
    int AwardedXp = 0, int RewardXp = 10);
public sealed record DialogueOutcome(DialogueView? Session = null, bool Missing = false, string? Error = null,
    bool PremiumRequired = false, bool Created = false);

internal sealed record DialogueGraph(string Title, Guid StartId, DialogueGraphNode[] Nodes);
internal sealed record DialogueGraphNode(Guid Id, string Text, bool IsTerminal, DialogueGraphChoice[] Choices);
internal sealed record DialogueGraphChoice(Guid Id, string Text, string Feedback, bool IsCorrect, Guid? NextNodeId);
