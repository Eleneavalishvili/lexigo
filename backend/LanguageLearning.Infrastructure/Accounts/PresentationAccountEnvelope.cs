using System.Security.Cryptography;
using System.Text.Json;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed record PresentationAccountEnvelope(string EncryptedKey, string Nonce, string Tag, string Ciphertext);

internal static class PresentationAccountEncryption
{
    internal static PresentationAccountEnvelope Encrypt(object accounts, string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        if (rsa.KeySize < 3072) throw new InvalidOperationException("Use a public key of at least 3072 bits.");
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(accounts, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        try
        {
            byte[] ciphertext = new byte[payload.Length];
            byte[] tag = new byte[16];
            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(nonce, payload, ciphertext, tag);
            return new(Convert.ToBase64String(rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256)),
                Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(ciphertext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(payload);
        }
    }
}
