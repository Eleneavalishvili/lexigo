using LanguageLearning.Infrastructure.Speech;
using LanguageLearning.Infrastructure.Storage;
using LanguageLearning.Tests.Storage;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Tests.Speech;

public sealed class LocalSpeechRecognizerTests
{
    [Theory]
    [InlineData("es", "vosk-model-small-es-0.42")]
    [InlineData("it", "vosk-model-small-it-0.22")]
    [InlineData("de", "vosk-model-small-de-0.15")]
    [InlineData("ru", "vosk-model-small-ru-0.22")]
    public void SupportedLanguagesUseFixedLocalNames(string language, string expected) =>
        Assert.Equal(expected, LocalSpeechLanguages.ModelDirectory(language));

    [Theory]
    [InlineData("../ru")]
    [InlineData("en")]
    [InlineData("https://example.test/model")]
    public void UnknownLanguagesCannotSelectPathsOrRemoteModels(string language) =>
        Assert.Null(LocalSpeechLanguages.ModelDirectory(language));

    [Fact]
    public async Task DisabledIsTheDefaultAndDoesNotLoadNativeModels()
    {
        using var recognizer = Create(new());
        var result = await recognizer.RecognizeAsync("es", Audio(), default);
        Assert.Equal(SpeechRecognitionStatus.Disabled, result.Status);
        Assert.Equal("", result.Text);
    }

    [Fact]
    public async Task MissingModelsFailWithoutTranscriptsOrRemoteFallback()
    {
        using var recognizer = Create(new() { Enabled = true, ModelsDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) });
        var result = await recognizer.RecognizeAsync("es", Audio(), default);
        Assert.Equal(SpeechRecognitionStatus.Unavailable, result.Status);
        Assert.Empty(result.Text);
    }

    [Theory]
    [InlineData(8000, 1, 16, 8000)]
    [InlineData(16000, 2, 16, 16000)]
    [InlineData(16000, 1, 8, 16000)]
    [InlineData(16000, 1, 16, 496000)]
    public async Task WrongPcmProfilesAndOverlongClipsAreRejected(int rate, int channels, int bits, int frames)
    {
        using var recognizer = Create(new() { Enabled = true, ModelsDirectory = Path.GetTempPath() });
        var audio = WaveAudioInspector.Inspect(WaveTestAudio.Create(frames, channels, bits, rate));
        await Assert.ThrowsAsync<InvalidDataException>(() => recognizer.RecognizeAsync("es", audio, default));
    }

    [Fact]
    public async Task SilenceIsNotAcceptedAsSpokenWords()
    {
        byte[] bytes = WaveTestAudio.Create(frames: 16000, sampleRate: 16000);
        Array.Clear(bytes, 44, bytes.Length - 44);
        using var recognizer = Create(new() { Enabled = true, ModelsDirectory = Path.GetTempPath() });
        var result = await recognizer.RecognizeAsync("es", WaveAudioInspector.Inspect(bytes), default);
        Assert.Equal(SpeechRecognitionStatus.NoSpeech, result.Status);
        Assert.Empty(result.Text);
    }

    [Fact]
    public async Task CancelledRequestsDoNotBeginNativeRecognition()
    {
        using var recognizer = Create(new() { Enabled = true, ModelsDirectory = Path.GetTempPath() });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recognizer.RecognizeAsync("ru", Audio(), cancellation.Token));
    }

    private static LocalSpeechRecognizer Create(LocalSpeechOptions options) => new(Options.Create(options));
    private static PreparedAudio Audio() => WaveAudioInspector.Inspect(WaveTestAudio.Create(frames: 16000, sampleRate: 16000));
}
