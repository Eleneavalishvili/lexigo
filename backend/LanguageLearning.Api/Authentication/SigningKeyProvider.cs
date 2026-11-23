using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LanguageLearning.Api.Authentication;

public sealed class SigningKeyProvider : IDisposable
{
    private readonly RSA rsa = RSA.Create();
    public RsaSecurityKey Key { get; }

    public SigningKeyProvider(IOptions<JwtOptions> options, IWebHostEnvironment environment)
    {
        string? pem = options.Value.SigningPrivateKeyPem;
        if (string.IsNullOrWhiteSpace(pem))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Configure a production RSA signing key through a secret store.");
            string path = Path.Combine(environment.ContentRootPath, ".local", "jwt-signing-key.pem");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path))
            {
                using var generated = RSA.Create(3072);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, generated.ExportRSAPrivateKeyPem());
                try { File.Move(temporary, path, overwrite: false); }
                catch (IOException) when (File.Exists(path)) { File.Delete(temporary); }
            }
            pem = File.ReadAllText(path);
        }
        rsa.ImportFromPem(pem);
        if (rsa.KeySize < 3072) throw new InvalidOperationException("RSA signing keys must be at least 3072 bits.");
        // Do not share cached signature providers across hosts that own separate RSA lifetimes.
        Key = new RsaSecurityKey(rsa)
        {
            KeyId = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))[..16],
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
    }

    public void Dispose() => rsa.Dispose();
}
