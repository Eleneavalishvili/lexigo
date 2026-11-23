using System.Buffers.Binary;
using LanguageLearning.Infrastructure.Storage;

namespace LanguageLearning.Tests.Storage;

public sealed class WaveAudioInspectorTests
{
    [Theory]
    [InlineData(1, 8, 8000)]
    [InlineData(1, 16, 44100)]
    [InlineData(2, 16, 48000)]
    public void ValidPcmPreservesSamplesAndDerivesDuration(int channels, int bits, int rate)
    {
        byte[] original = WaveTestAudio.Create(frames: rate, channels: channels, bits: bits, sampleRate: rate);
        var audio = WaveAudioInspector.Inspect(original);
        Assert.Equal(original, audio.Bytes.ToArray());
        Assert.Equal(1000, audio.DurationMilliseconds);
        Assert.Equal(channels, audio.Channels);
        Assert.Equal(bits, audio.BitsPerSample);
        Assert.Equal(rate, audio.SampleRate);
        Assert.Equal("audio/wav", audio.MimeType);
    }

    [Fact]
    public void MetadataAndEmptyFormatExtensionAreRemovedWithoutChangingSamples()
    {
        var original = WaveTestAudio.Create(metadata: true, extendedFormat: true);
        var prepared = WaveAudioInspector.Inspect(original);
        Assert.Equal(WaveTestAudio.Create(), prepared.Bytes.ToArray());
        Assert.DoesNotContain("owner", System.Text.Encoding.ASCII.GetString(prepared.Bytes.Span));
    }

    [Fact]
    public void OddSamplePayloadHasCanonicalZeroPadding()
    {
        byte[] original = WaveTestAudio.Create(frames: 801, bits: 8);
        original[^1] = 255;
        var prepared = WaveAudioInspector.Inspect(original);
        Assert.Equal(101, prepared.DurationMilliseconds);
        Assert.Equal(0, prepared.Bytes.Span[^1]);
        Assert.Equal(original.AsSpan(44, 801).ToArray(), prepared.Bytes.Slice(44, 801).ToArray());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 0)]
    [InlineData(4, 0)]
    [InlineData(16, uint.MaxValue)]
    [InlineData(28, 123)]
    [InlineData(32, 0)]
    [InlineData(40, uint.MaxValue)]
    public void InvalidSignaturesSizesRatesAndAlignmentFailCleanly(int offset, uint value)
    {
        byte[] bytes = WaveTestAudio.Create();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(43)]
    [InlineData(100)]
    public void TruncatedFilesAreRejected(int length)
    {
        byte[] bytes = WaveTestAudio.Create()[..length];
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(bytes));
    }

    [Theory]
    [InlineData(3, 16, 8000, 1)]
    [InlineData(1, 24, 8000, 1)]
    [InlineData(1, 16, 7999, 1)]
    [InlineData(1, 16, 48001, 1)]
    [InlineData(1, 16, 8000, 3)]
    public void UnsupportedChannelsBitsRatesAndCodecsAreRejected(int channels, int bits, int rate, ushort encoding)
    {
        byte[] bytes = WaveTestAudio.Create(channels: channels, bits: bits, sampleRate: rate, encoding: encoding);
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(799)]
    [InlineData(960001)]
    public void EmptyShortAndOverlongAudioIsRejected(int frames)
    {
        byte[] bytes = WaveTestAudio.Create(frames: frames);
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(bytes));
    }

    [Fact]
    public void ExactMaximumDurationIsAccepted()
    {
        Assert.Equal(120000, WaveAudioInspector.Inspect(WaveTestAudio.Create(frames: 960000)).DurationMilliseconds);
    }

    [Fact]
    public void TrailingPayloadCannotBeSmuggledPastTheRiffSize()
    {
        byte[] bytes = [.. WaveTestAudio.Create(), .. "<script>payload</script>"u8];
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(bytes));
    }

    [Fact]
    public void DuplicateAndOutOfOrderChunksAreRejected()
    {
        byte[] valid = WaveTestAudio.Create();
        byte[] duplicateFormat = [.. valid, .. valid.AsSpan(12, 24)];
        FixRiffSize(duplicateFormat);
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(duplicateFormat));
        byte[] duplicateData = [.. valid, .. valid.AsSpan(36)];
        FixRiffSize(duplicateData);
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(duplicateData));
        byte[] outOfOrder = [.. valid.AsSpan(0, 12), .. valid.AsSpan(36), .. valid.AsSpan(12, 24)];
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(outOfOrder));
    }

    [Fact]
    public void IncompleteFrameAndMissingPaddingAreRejected()
    {
        byte[] incomplete = WaveTestAudio.Create();
        BinaryPrimitives.WriteUInt32LittleEndian(incomplete.AsSpan(40, 4), 1599);
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(incomplete));
        byte[] missingPadding = WaveTestAudio.Create(frames: 801, bits: 8)[..^1];
        FixRiffSize(missingPadding);
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(missingPadding));
    }

    [Fact]
    public void ExcessiveMetadataChunkCountsAreBounded()
    {
        var valid = WaveTestAudio.Create();
        using var buffer = new MemoryStream();
        buffer.Write(valid.AsSpan(0, 12));
        for (int index = 0; index < 128; index++) buffer.Write("JUNK\0\0\0\0"u8);
        buffer.Write(valid.AsSpan(12));
        byte[] bytes = buffer.ToArray();
        FixRiffSize(bytes);
        Assert.Throws<InvalidDataException>(() => WaveAudioInspector.Inspect(bytes));
    }

    [Fact]
    public async Task StreamReadIgnoresReportedLengthAndEnforcesActualByteLimit()
    {
        using var source = new NonSeekableStream(new byte[WaveAudioInspector.MaximumBytes + 1]);
        await Assert.ThrowsAsync<InvalidDataException>(() => WaveAudioInspector.ReadAsync(source));
        Assert.Equal(WaveAudioInspector.MaximumBytes + 1, source.BytesRead);
    }

    [Fact]
    public async Task PartialReadsWorkWithoutTakingOwnershipOfTheInput()
    {
        using var source = new NonSeekableStream(WaveTestAudio.Create(metadata: true), maximumRead: 7);
        var audio = await WaveAudioInspector.ReadAsync(source);
        Assert.Equal(WaveTestAudio.Create(), audio.Bytes.ToArray());
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task CancelledReadDoesNotContinueProcessing()
    {
        using var source = new NonSeekableStream(WaveTestAudio.Create());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WaveAudioInspector.ReadAsync(source, cancellation.Token));
        Assert.Equal(0, source.BytesRead);
    }

    private static void FixRiffSize(byte[] bytes) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), (uint)(bytes.Length - 8));

    private sealed class NonSeekableStream(byte[] bytes, int maximumRead = int.MaxValue) : Stream
    {
        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int length = Math.Min(Math.Min(count, maximumRead), bytes.Length - BytesRead);
            bytes.AsSpan(BytesRead, length).CopyTo(buffer.AsSpan(offset));
            BytesRead += length;
            return length;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = Math.Min(Math.Min(buffer.Length, maximumRead), bytes.Length - BytesRead);
            bytes.AsSpan(BytesRead, length).CopyTo(buffer.Span);
            BytesRead += length;
            return ValueTask.FromResult(length);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
