namespace LanguageLearning.Infrastructure.Speech;

public static class LocalSpeechLanguages
{
    public static string? ModelDirectory(string language) => language switch
    {
        "es" => "vosk-model-small-es-0.42",
        "it" => "vosk-model-small-it-0.22",
        "de" => "vosk-model-small-de-0.15",
        "ru" => "vosk-model-small-ru-0.22",
        _ => null
    };
}
