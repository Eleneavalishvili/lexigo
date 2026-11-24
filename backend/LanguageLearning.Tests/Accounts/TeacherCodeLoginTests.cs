using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class TeacherCodeLoginTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task EightUniqueTeacherCodesRequireMfaAndNeverAppearInPublicProfiles()
    {
        fixture.Services.GetRequiredService<IConfiguration>()["TeacherAccess:LookupKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var rsa = RSA.Create(3072);
        PresentationAccountEnvelope envelope;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var provisioner = scope.ServiceProvider.GetRequiredService<PresentationAccountProvisioner>();
            await provisioner.CreateAsync(rsa.ExportSubjectPublicKeyInfoPem(), default);
            envelope = await provisioner.ExtendTutorsAsync(rsa.ExportSubjectPublicKeyInfoPem(), default);
        }
        byte[] key = rsa.Decrypt(Convert.FromBase64String(envelope.EncryptedKey), RSAEncryptionPadding.OaepSHA256);
        byte[] ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        byte[] payload = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, 16)) aes.Decrypt(Convert.FromBase64String(envelope.Nonce), ciphertext, Convert.FromBase64String(envelope.Tag), payload);
        using var tutors = JsonDocument.Parse(payload);
        Assert.Equal(8, tutors.RootElement.GetArrayLength());
        Assert.Equal(8, tutors.RootElement.EnumerateArray().Select(tutor => tutor.GetProperty("teacherAccessCode").GetString()).Distinct().Count());
        await using var verification = fixture.Services.CreateAsyncScope();
        var database = verification.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(11, await database.Users.CountAsync());
        Assert.Equal(8, await database.Users.CountAsync(user => user.TeacherAccessCodeHash != null));
        Assert.Equal(8, await database.ExtraStudyOffers.CountAsync(offer => offer.PricePerLesson > 0 && offer.PortraitKey != "default"));
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        foreach (var tutor in tutors.RootElement.EnumerateArray())
        {
            string accessCode = tutor.GetProperty("teacherAccessCode").GetString()!;
            Assert.Matches("^[0-9]{6}$", accessCode);
            string authenticator = TestAuthenticator.Code(tutor.GetProperty("authenticatorKey").GetString()!);
            var body = new { accessCode, authenticatorCode = authenticator };
            var response = await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/teacher-login", body, csrf: true);
            response.EnsureSuccessStatusCode();
            using var ticket = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var account = ticket.RootElement.GetProperty("user");
            Assert.Contains(account.GetProperty("roles").EnumerateArray(), role => role.GetString() == "Teacher");
            Assert.DoesNotContain("teacherAccessCode", account.GetRawText(), StringComparison.OrdinalIgnoreCase);
            string token = ticket.RootElement.GetProperty("accessToken").GetString()!;
            (await browser.SendAsync(HttpMethod.Get, "/api/v1/classes", bearer: token)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Post,
                "/api/v1/auth/teacher-login", body, csrf: true)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(HttpMethod.Post,
            "/api/v1/auth/teacher-login", new { accessCode = "000000", authenticatorCode = "000000" }, csrf: true)).StatusCode);
    }
}
