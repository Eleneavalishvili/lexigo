using LanguageLearning.Infrastructure.Vocabulary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Vocabulary;

[ApiController]
[Authorize]
[Route("api/v1/me/export/vocabulary")]
[EnableRateLimiting("account-entry")]
public sealed class VocabularyExportController(VocabularyExport export) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    public async Task Download(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentDisposition = "attachment; filename=\"lexigo-vocabulary.json\"";
        Response.ContentType = "application/json; charset=utf-8";
        await export.WriteAsync(Guid.Parse(User.FindFirst("sub")!.Value), Response.Body, cancellationToken);
    }
}
