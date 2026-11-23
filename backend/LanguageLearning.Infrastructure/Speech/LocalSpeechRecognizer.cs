using System.Buffers.Binary;
using System.Text.Json;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Vosk;

namespace LanguageLearning.Infrastructure.Speech;

public sealed class LocalSpeechRecognizer(IOptions<LocalSpeechOptions> options) : ISpeechRecognizer, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Model? model;
    private string? loadedLanguage;

    public async Task<SpeechRecognitionResult> RecognizeAsync(string language, PreparedAudio audio, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Value.Enabled) return new(SpeechRecognitionStatus.Disabled);
        string? directory = LocalSpeechLanguages.ModelDirectory(language);
        if (directory is null || !Path.IsPathFullyQualified(options.Value.ModelsDirectory))
            return new(SpeechRecognitionStatus.Unavailable);
        if (audio.SampleRate != 16000 || audio.Channels != 1 || audio.BitsPerSample != 16 || audio.DurationMilliseconds > 30000)
            throw new InvalidDataException("Speech checking needs mono 16-bit PCM WAV at 16000 Hz, up to 30 seconds.");
        if (!HasSignal(audio.Bytes.Span[44..])) return new(SpeechRecognitionStatus.NoSpeech);
        if (!await gate.WaitAsync(0, cancellationToken)) return new(SpeechRecognitionStatus.Busy);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string modelPath = Path.Combine(options.Value.ModelsDirectory, directory);
            if (!File.Exists(Path.Combine(modelPath, "am", "final.mdl"))
                || !File.Exists(Path.Combine(modelPath, "conf", "model.conf")))
                return new(SpeechRecognitionStatus.Unavailable);
            if (model is null || loadedLanguage != language)
            {
                model?.Dispose();
                model = null;
                loadedLanguage = null;
                Vosk.Vosk.SetLogLevel(-1);
                model = new Model(modelPath);
                loadedLanguage = language;
            }
            // No expected-answer grammar: constraining recognition to the correct word can manufacture matches.
            using var recognizer = new VoskRecognizer(model, 16000);
            var segments = new List<string>();
            var samples = audio.Bytes[44..];
            for (int offset = 0; offset < samples.Length; offset += 8000)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] block = samples.Slice(offset, Math.Min(8000, samples.Length - offset)).ToArray();
                if (recognizer.AcceptWaveform(block, block.Length)) AddSegment(recognizer.Result(), segments);
            }
            AddSegment(recognizer.FinalResult(), segments);
            cancellationToken.ThrowIfCancellationRequested();
            string text = string.Join(' ', segments);
            return string.IsNullOrWhiteSpace(text) ? new(SpeechRecognitionStatus.NoSpeech)
                : new(SpeechRecognitionStatus.Recognized, text);
        }
        catch (Exception error) when (error is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or IOException or JsonException)
        {
            return new(SpeechRecognitionStatus.Unavailable);
        }
        finally
        {
            gate.Release();
        }
    }

    private static void AddSegment(string json, List<string> segments)
    {
        using var result = JsonDocument.Parse(json);
        if (result.RootElement.TryGetProperty("text", out var value) && value.GetString() is { Length: > 0 and <= 2000 } text)
            segments.Add(text);
    }

    private static bool HasSignal(ReadOnlySpan<byte> samples)
    {
        double squares = 0;
        for (int offset = 0; offset < samples.Length; offset += 2)
        {
            double value = BinaryPrimitives.ReadInt16LittleEndian(samples.Slice(offset, 2)) / 32768.0;
            squares += value * value;
        }
        return samples.Length > 0 && Math.Sqrt(squares / (samples.Length / 2)) >= 0.003;
    }

    public void Dispose()
    {
        model?.Dispose();
        gate.Dispose();
    }
}
