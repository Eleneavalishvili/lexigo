namespace LanguageLearning.Infrastructure.Speech;

public sealed class LocalSpeechOptions
{
    public bool Enabled { get; set; }
    public string ModelsDirectory { get; set; } = "";
}
