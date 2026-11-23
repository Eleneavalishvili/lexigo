using System.Buffers.Binary;
using System.IO.Compression;

namespace LanguageLearning.Infrastructure.Accounts;

public static class AvatarPng
{
    public const int Side = 128;
    private const int BytesPerRow = Side * 4;
    public const int PixelBytes = Side * BytesPerRow;

    public static byte[] Encode(ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != PixelBytes)
            throw new ArgumentException("Avatar pixels must be a 128 × 128 RGBA image.", nameof(pixels));

        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        Span<byte> header = stackalloc byte[13];
        header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, Side);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], Side);
        header[8] = 8;
        header[9] = 6; // RGBA
        WriteChunk(png, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (int row = 0; row < Side; row++)
            {
                zlib.WriteByte(0); // No scanline filter.
                zlib.Write(pixels.Slice(row * BytesPerRow, BytesPerRow));
            }
        }

        WriteChunk(png, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> integer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(integer, data.Length);
        output.Write(integer);
        output.Write(type);
        output.Write(data);

        uint checksum = UpdateCrc(UpdateCrc(uint.MaxValue, type), data);
        BinaryPrimitives.WriteUInt32BigEndian(integer, ~checksum);
        output.Write(integer);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc;
    }
}
