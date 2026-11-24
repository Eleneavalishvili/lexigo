using System.Security.Cryptography;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

public sealed partial class PresentationAccountProvisioner
{
    public async Task<PresentationAccountEnvelope> ExtendTutorsAsync(string publicKeyPem, CancellationToken token)
    {
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(publicKeyPem);
            if (rsa.KeySize < 3072) throw new InvalidOperationException("Use a 3072-bit public key.");
        }
        if (teacherCodes.Hash("000000") is null) throw new InvalidOperationException("Configure the private teacher lookup key first.");
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730027001)", token);
        var profiles = new[]
        {
            new TutorProfile("tutor-es", "Ava", 2, "ava", 20m, "A1", "B2"),
            new TutorProfile("tutor-es-2", "Leo", 2, "leo", 24m, "A1", "C1"),
            new TutorProfile("tutor-it", "Sofia", 3, "sofia", 22m, "A1", "B2"),
            new TutorProfile("tutor-it-2", "Luca", 3, "luca", 25m, "A1", "C1"),
            new TutorProfile("tutor-de", "Nina", 4, "nina", 25m, "A1", "C1"),
            new TutorProfile("tutor-de-2", "Noah", 4, "noah", 28m, "A2", "C1"),
            new TutorProfile("tutor-ru", "Maya", 5, "maya", 20m, "A1", "B2"),
            new TutorProfile("tutor-ru-2", "Alex", 5, "alex", 23m, "A1", "C1")
        };
        var credentials = new List<TutorCredential>();
        foreach (var profile in profiles)
        {
            string email = profile.Account + Domain;
            var user = await users.FindByEmailAsync(email);
            string? password = null;
            string[]? recovery = null;
            if (user is null)
            {
                password = "Lg!9" + RandomNumberGenerator.GetHexString(40);
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    DisplayName = "Presentation tutor " + profile.Name,
                    EmailConfirmed = true,
                    CreatedAt = clock.GetUtcNow()
                };
                Ensure(await users.CreateAsync(user, password));
                Ensure(await users.AddToRolesAsync(user, [RoleNames.Learner, RoleNames.Teacher]));
                Ensure(await users.ResetAuthenticatorKeyAsync(user));
                Ensure(await users.SetTwoFactorEnabledAsync(user, true));
                recovery = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))!.ToArray();
                database.UserPreferences.Add(new UserPreference { UserId = user.Id, TimeZone = "Asia/Tbilisi" });
                database.UserLanguageScopes.Add(new UserLanguageScope { UserId = user.Id, LanguageId = profile.Language });
                var course = await database.Courses.SingleAsync(item => item.SourceLanguageId == 1 && item.TargetLanguageId == profile.Language, token);
                database.ExtraStudyOffers.Add(new ExtraStudyOffer { TeacherId = user.Id, CourseId = course.Id, Enabled = true });
                database.Classrooms.Add(new Classroom
                {
                    TeacherId = user.Id,
                    CourseId = course.Id,
                    Name = "Presentation tutor " + profile.Name + " · practice class",
                    Description = "Presentation classroom. Ask its teacher for an invitation."
                });
                await database.SaveChangesAsync(token);
            }
            else if (user.TeacherAccessCodeHash is not null || !await users.IsInRoleAsync(user, RoleNames.Teacher)
                || !user.TwoFactorEnabled || user.Status != "Active" || !user.EmailConfirmed)
                throw new InvalidOperationException("Tutor access already exists or an account is ineligible. Existing access will not be overwritten.");

            string accessCode;
            string hash;
            do
            {
                accessCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
                hash = teacherCodes.Hash(accessCode)!;
            } while (await database.Users.AnyAsync(item => item.TeacherAccessCodeHash == hash, token));
            user.TeacherAccessCodeHash = hash;
            user.DisplayName = "Presentation tutor " + profile.Name;
            Ensure(await users.UpdateAsync(user));
            var offer = await database.ExtraStudyOffers.SingleAsync(item => item.TeacherId == user.Id, token);
            offer.PricePerLesson = profile.Price;
            offer.LevelFrom = profile.From;
            offer.LevelTo = profile.To;
            offer.PortraitKey = profile.Portrait;
            offer.Biography = $"Presentation tutor profile for {profile.From}–{profile.To} conversation, vocabulary and guided lesson practice.";
            database.AuditLogs.Add(new AuditLog
            {
                ActorId = null,
                Action = "PresentationTutorProfileProvisioned",
                EntityType = "User",
                EntityId = user.Id.ToString(),
                Reason = "Approved fictional presentation tutor profile and protected code access.",
                TraceId = Guid.NewGuid().ToString("N"),
                OccurredAt = clock.GetUtcNow()
            });
            credentials.Add(new(email, user.DisplayName, accessCode, (await users.GetAuthenticatorKeyAsync(user))!, password, recovery));
            await database.SaveChangesAsync(token);
        }
        var envelope = PresentationAccountEncryption.Encrypt(credentials, publicKeyPem);
        await transaction.CommitAsync(token);
        return envelope;
    }

    private sealed record TutorProfile(string Account, string Name, int Language, string Portrait, decimal Price, string From, string To);
    private sealed record TutorCredential(string Email, string DisplayName, string TeacherAccessCode,
        string AuthenticatorKey, string? Password, string[]? RecoveryCodes);
}
