namespace LanguageLearning.Core.Content;

public static class ExerciseFormats
{
    public static bool IsChoice(string type, int optionCount = 0) => type is "MultipleChoice" or "Reading" or "DialogueChoice" or "Alphabet"
        || type == "Listening" && optionCount > 0;
    public static bool HasContext(string type) => type is "Reading" or "DialogueChoice";
    public static bool SupportsLearning(string type) => IsChoice(type) || type is "Translation" or "FillBlank" or "SentenceBuilder" or "Matching" or "Listening" or "Speaking" or "SpeakingCheck";
    public static bool IsKnown(string type) => SupportsLearning(type);
}
