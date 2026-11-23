using System.Text;

namespace LanguageLearning.Tests.Storage;

internal static class WaveTestAudio
{
    public static byte[] Create(int frames = 800, int channels = 1, int bits = 16, int sampleRate = 8000,
        ushort encoding = 1, bool metadata = false, bool extendedFormat = false)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(0);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(extendedFormat ? 18 : 16);
        writer.Write(encoding);
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bits / 8);
        writer.Write((ushort)(channels * bits / 8));
        writer.Write((ushort)bits);
        if (extendedFormat) writer.Write((ushort)0);
        if (metadata)
        {
            writer.Write("LIST"u8);
            writer.Write(5);
            writer.Write("owner"u8);
            writer.Write((byte)0);
        }
        writer.Write("data"u8);
        int length = frames * channels * bits / 8;
        writer.Write(length);
        for (int index = 0; index < length; index++) writer.Write((byte)(index % 251));
        if (length % 2 != 0) writer.Write((byte)0);
        output.Position = 4;
        writer.Write((uint)(output.Length - 8));
        return output.ToArray();
    }
}
