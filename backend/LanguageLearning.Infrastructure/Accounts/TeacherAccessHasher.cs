using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed class TeacherAccessHasher(IConfiguration configuration)
{
    public string? Hash(string code)
    {
        if (code.Length != 6 || code.Any(letter => !char.IsAsciiDigit(letter))) return null;
        string? encodedKey = configuration["TeacherAccess:LookupKey"];
        if (string.IsNullOrWhiteSpace(encodedKey)) return null;
        byte[] key;
        try { key = Convert.FromBase64String(encodedKey); }
        catch (FormatException) { return null; }
        try
        {
            return key.Length < 32 ? null : Convert.ToHexString(HMACSHA256.HashData(key,
                Encoding.UTF8.GetBytes("lexigo-teacher-access:" + code)));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
