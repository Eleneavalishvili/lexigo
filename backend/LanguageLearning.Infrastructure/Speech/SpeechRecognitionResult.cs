namespace LanguageLearning.Infrastructure.Speech;

public enum SpeechRecognitionStatus
{
    Disabled,
    Unavailable,
    Busy,
    NoSpeech,
    Recognized
}

// A transcript is evidence of recognized words, not a pronunciation-quality score.
public sealed record SpeechRecognitionResult(SpeechRecognitionStatus Status, string Text = "");
