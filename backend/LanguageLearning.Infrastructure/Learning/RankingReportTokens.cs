using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class RankingReportTokens(IDataProtectionProvider protection, TimeProvider clock)
{
    private readonly IDataProtector protector = protection.CreateProtector("LexiGo.RankingNameReport.v1");

    internal string Issue(Guid userId, Guid courseId, DateOnly week, string displayName) =>
        protector.Protect(JsonSerializer.Serialize(new RankingReportSubject(
            userId, courseId, week, displayName, clock.GetUtcNow().AddMinutes(10))));

    internal RankingReportSubject? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return null;
        try
        {
            var subject = JsonSerializer.Deserialize<RankingReportSubject>(protector.Unprotect(token));
            return subject?.ExpiresAt > clock.GetUtcNow() ? subject : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException)
        {
            return null;
        }
    }
}

internal sealed record RankingReportSubject(Guid UserId, Guid CourseId, DateOnly Week,
    string DisplayName, DateTimeOffset ExpiresAt);
