namespace LanguageLearning.Infrastructure.Storage;

public sealed class PreparedAudio
{
    internal PreparedAudio(byte[] bytes, int durationMilliseconds, int sampleRate, int channels, int bitsPerSample)
    {
        Bytes = bytes;
        DurationMilliseconds = durationMilliseconds;
        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
    }

    public ReadOnlyMemory<byte> Bytes { get; }
    public string MimeType => "audio/wav";
    public int DurationMilliseconds { get; }
    public int SampleRate { get; }
    public int Channels { get; }
    public int BitsPerSample { get; }
}
