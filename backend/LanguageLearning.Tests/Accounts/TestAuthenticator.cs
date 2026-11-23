using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace LanguageLearning.Tests.Accounts;

internal static class TestAuthenticator
{
    // RFC 6238 test client, independent of the application's Identity verifier.
    public static string Code(string key, long offset = 0)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var decoded = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (char letter in key.TrimEnd('='))
        {
            buffer = (buffer << 5) | alphabet.IndexOf(letter);
            bits += 5;
            if (bits >= 8) { bits -= 8; decoded.Add((byte)(buffer >> bits)); }
        }
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + offset);
        byte[] digest = HMACSHA1.HashData(decoded.ToArray(), counter);
        int index = digest[^1] & 15;
        int number = BinaryPrimitives.ReadInt32BigEndian(digest.AsSpan(index, 4)) & 0x7fffffff;
        return (number % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }
}
