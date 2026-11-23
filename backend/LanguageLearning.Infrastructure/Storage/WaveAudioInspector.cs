using System.Buffers.Binary;

namespace LanguageLearning.Infrastructure.Storage;

// Parses a deliberately narrow PCM profile and rebuilds it without supplied metadata chunks.
public static class WaveAudioInspector
{
    public const int MaximumBytes = 10 * 1024 * 1024;
    public const int MaximumDurationSeconds = 120;
    private const int MaximumChunks = 128;

    public static async Task<PreparedAudio> ReadAsync(Stream input, CancellationToken cancellationToken = default,
        int maximumBytes = MaximumBytes)
    {
        if (!input.CanRead) throw InvalidAudio();
        using var buffer = new MemoryStream();
        byte[] block = new byte[81920];
        while (true)
        {
            // Read one byte beyond the limit to detect overflow; never allocate an unbounded body.
            int remaining = maximumBytes + 1 - (int)buffer.Length;
            int read = await input.ReadAsync(block.AsMemory(0, Math.Min(block.Length, remaining)), cancellationToken);
            if (read == 0) break;
            buffer.Write(block, 0, read);
            if (buffer.Length > maximumBytes)
                throw new InvalidDataException($"Audio must be no larger than {maximumBytes / (1024 * 1024)} MiB.");
        }
        return Inspect(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    public static PreparedAudio Inspect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 44 || bytes.Length > MaximumBytes || !bytes[..4].SequenceEqual("RIFF"u8)
            || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8) || Read32(bytes, 4) != bytes.Length - 8)
            throw InvalidAudio();

        int offset = 12;
        int chunkCount = 0;
        ReadOnlySpan<byte> format = default;
        ReadOnlySpan<byte> samples = default;
        while (offset < bytes.Length)
        {
            if (++chunkCount > MaximumChunks || bytes.Length - offset < 8) throw InvalidAudio();
            var id = bytes.Slice(offset, 4);
            uint size = Read32(bytes, offset + 4);
            long paddedSize = (long)size + (size & 1);
            if (paddedSize > bytes.Length - offset - 8) throw InvalidAudio();
            var payload = bytes.Slice(offset + 8, (int)size);
            if (id.SequenceEqual("fmt "u8))
            {
                if (!format.IsEmpty || !samples.IsEmpty || size is not (16 or 18)) throw InvalidAudio();
                if (size == 18 && Read16(payload, 16) != 0) throw InvalidAudio();
                format = payload[..16];
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (format.IsEmpty || !samples.IsEmpty || size == 0) throw InvalidAudio();
                samples = payload;
            }
            // All other chunks are bounded and discarded, including recorder-identifying metadata.
            offset += 8 + (int)paddedSize;
        }
        if (format.IsEmpty || samples.IsEmpty) throw InvalidAudio();
        return Prepare(format, samples);
    }

    private static PreparedAudio Prepare(ReadOnlySpan<byte> format, ReadOnlySpan<byte> samples)
    {
        ushort encoding = Read16(format, 0);
        ushort channels = Read16(format, 2);
        uint sampleRate = Read32(format, 4);
        uint byteRate = Read32(format, 8);
        ushort alignment = Read16(format, 12);
        ushort bits = Read16(format, 14);
        if (encoding != 1 || channels is not (1 or 2) || bits is not (8 or 16) || sampleRate is < 8000 or > 48000)
            throw new InvalidDataException("Use PCM WAV with one or two channels, 8- or 16-bit samples and an 8–48 kHz sample rate.");
        int expectedAlignment = channels * (bits / 8);
        if (alignment != expectedAlignment || byteRate != sampleRate * expectedAlignment || samples.Length % alignment != 0)
            throw InvalidAudio();
        long frames = samples.Length / alignment;
        if (frames * 10 < sampleRate || frames > (long)sampleRate * MaximumDurationSeconds)
            throw new InvalidDataException("Audio must last between 0.1 and 120 seconds.");

        int padding = samples.Length & 1;
        byte[] canonical = new byte[44 + samples.Length + padding];
        "RIFF"u8.CopyTo(canonical);
        Write32(canonical, 4, (uint)(canonical.Length - 8));
        "WAVEfmt "u8.CopyTo(canonical.AsSpan(8));
        Write32(canonical, 16, 16);
        format.CopyTo(canonical.AsSpan(20));
        "data"u8.CopyTo(canonical.AsSpan(36));
        Write32(canonical, 40, (uint)samples.Length);
        samples.CopyTo(canonical.AsSpan(44));
        return new PreparedAudio(canonical, (int)((frames * 1000 + sampleRate - 1) / sampleRate), (int)sampleRate, channels, bits);
    }

    private static InvalidDataException InvalidAudio() => new("Upload a complete PCM WAV file with valid format and audio chunks.");
    private static ushort Read16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    private static uint Read32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static void Write32(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset, 4), value);
}
