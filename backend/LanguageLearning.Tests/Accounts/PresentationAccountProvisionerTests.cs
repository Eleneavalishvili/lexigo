using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class PresentationAccountProvisionerTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task ProvisioningEncryptsUniqueCredentialsAndCreatesMfaTeachersWithoutGrantingAdmin()
    {
        using var rsa = RSA.Create(3072);
        PresentationAccountEnvelope envelope;
        await using (var scope = fixture.Services.CreateAsyncScope())
            envelope = await scope.ServiceProvider.GetRequiredService<PresentationAccountProvisioner>()
                .CreateAsync(rsa.ExportSubjectPublicKeyInfoPem(), default);
        byte[] key = rsa.Decrypt(Convert.FromBase64String(envelope.EncryptedKey), RSAEncryptionPadding.OaepSHA256);
        byte[] ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        byte[] payload = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, 16)) aes.Decrypt(Convert.FromBase64String(envelope.Nonce),
            ciphertext, Convert.FromBase64String(envelope.Tag), payload);
        using var accounts = JsonDocument.Parse(payload);
        Assert.Equal(7, accounts.RootElement.GetArrayLength());
        Assert.Equal(7, accounts.RootElement.EnumerateArray().Select(account => account.GetProperty("password").GetString()).Distinct().Count());
        var tutor = accounts.RootElement[0];
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        using var login = await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/login", new
        {
            email = tutor.GetProperty("email").GetString(),
            password = tutor.GetProperty("password").GetString(),
            recoveryCode = tutor.GetProperty("recoveryCodes")[0].GetString()
        }, csrf: true);
        login.EnsureSuccessStatusCode();
        using var ticket = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.Contains(ticket.RootElement.GetProperty("user").GetProperty("roles").EnumerateArray(), role => role.GetString() == "Teacher");
        await using var verification = fixture.Services.CreateAsyncScope();
        var database = verification.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(4, await database.ExtraStudyOffers.CountAsync());
        Assert.Equal(4, await database.Classrooms.CountAsync());
        var operatorEmail = "operator" + PresentationAccountProvisioner.Domain;
        var operatorId = await database.Users.Where(user => user.Email == operatorEmail).Select(user => user.Id).SingleAsync();
        var roles = await (from membership in database.UserRoles
                           join role in database.Roles on membership.RoleId equals role.Id
                           where membership.UserId == operatorId
                           select role.Name).ToArrayAsync();
        Assert.DoesNotContain("Admin", roles);
        Assert.DoesNotContain("SuperAdmin", roles);
        Assert.Equal(7, await database.Users.CountAsync(user => user.Email != null && user.Email.EndsWith(PresentationAccountProvisioner.Domain)
            && user.TwoFactorEnabled && user.EmailConfirmed));
        Assert.Empty(await database.EmailOutboxMessages.Where(message => message.Recipient.EndsWith(PresentationAccountProvisioner.Domain)).ToArrayAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => verification.ServiceProvider.GetRequiredService<PresentationAccountProvisioner>()
            .CreateAsync(rsa.ExportSubjectPublicKeyInfoPem(), default));
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(payload);
    }

    [Fact]
    public async Task WeakEncryptionKeyCannotCreateOrPromoteAccounts()
    {
        using var rsa = RSA.Create(2048);
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        int before = await database.Users.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<PresentationAccountProvisioner>()
            .CreateAsync(rsa.ExportSubjectPublicKeyInfoPem(), default));
        Assert.Equal(before, await database.Users.CountAsync());
    }
}
