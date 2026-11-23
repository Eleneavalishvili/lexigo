using System.Buffers.Binary;
using System.IO.Compression;
using LanguageLearning.Infrastructure.Accounts;

namespace LanguageLearning.Tests.Accounts;

public sealed class AvatarPngTests
{
    [Fact]
    public void EncodesBoundedPixelsWithoutExtraChunks()
    {
        byte[] pixels = new byte[AvatarPng.PixelBytes];
        new Random(42).NextBytes(pixels);
        byte[] png = AvatarPng.Encode(pixels);
        Assert.Equal(Convert.FromHexString("89504E470D0A1A0A"), png[..8]);
        Assert.Equal(128, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(128, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
        Assert.Equal(Convert.FromHexString("0000000049454E44AE426082"), png[^12..]);

        int compressedLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33));
        Assert.Equal(png.Length - 57, compressedLength);
        Assert.True(png.AsSpan(37, 4).SequenceEqual("IDAT"u8));
        using var compressed = new MemoryStream(png, 41, compressedLength);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        const int rowBytes = 128 * 4;
        var scanline = new byte[rowBytes + 1];
        for (int row = 0; row < 128; row++)
        {
            zlib.ReadExactly(scanline);
            Assert.Equal(0, scanline[0]);
            Assert.True(scanline.AsSpan(1).SequenceEqual(pixels.AsSpan(row * rowBytes, rowBytes)));
        }
        Assert.Equal(-1, zlib.ReadByte());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65535)]
    [InlineData(65537)]
    public void RejectsEveryOtherPayloadSize(int length) =>
        Assert.Throws<ArgumentException>(() => AvatarPng.Encode(new byte[length]));
}
