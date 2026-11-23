using LanguageLearning.Infrastructure.Storage;

namespace LanguageLearning.Infrastructure.Speech;

public interface ISpeechRecognizer
{
    Task<SpeechRecognitionResult> RecognizeAsync(string language, PreparedAudio audio, CancellationToken cancellationToken);
}
