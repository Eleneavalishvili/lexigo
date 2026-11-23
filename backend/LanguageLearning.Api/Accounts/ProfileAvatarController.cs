using LanguageLearning.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Accounts;

[ApiController]
[Authorize]
[Route("api/v1/me/avatar")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileAvatarController(ProfileAvatarService avatars) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    [EnableRateLimiting("content-read")]
    public async Task<IActionResult> Read(CancellationToken cancellationToken)
    {
        var url = await avatars.ReadAsync(UserId, cancellationToken);
        return url is null ? NoContent() : Ok(new { url });
    }

    [HttpPut]
    [EnableRateLimiting("media-upload")]
    [RequestSizeLimit(AvatarPng.PixelBytes)]
    [Consumes("application/octet-stream")]
    public async Task<IActionResult> Save(CancellationToken cancellationToken)
    {
        if (Request.ContentLength is { } length && length != AvatarPng.PixelBytes)
            return Problem(statusCode: 400, title: "Choose a valid cropped avatar.");
        var pixels = new byte[AvatarPng.PixelBytes];
        int read = await Request.Body.ReadAtLeastAsync(pixels, pixels.Length, throwOnEndOfStream: false, cancellationToken);
        if (read != pixels.Length || await Request.Body.ReadAsync(new byte[1], cancellationToken) != 0)
            return Problem(statusCode: 400, title: "Choose a valid cropped avatar.");
        return (await avatars.SaveAsync(UserId, FamilyId, AvatarPng.Encode(pixels), cancellationToken)) switch
        {
            AvatarWriteResult.Saved => NoContent(),
            AvatarWriteResult.Missing => NotFound(),
            AvatarWriteResult.LimitReached => Problem(statusCode: 409, title: "Wait for older avatar uploads to finish clearing."),
            _ => Problem(statusCode: 503, title: "Avatar storage is unavailable. Try again later.")
        };
    }

    [HttpDelete]
    [EnableRateLimiting("account-entry")]
    public async Task<IActionResult> Remove(CancellationToken cancellationToken) =>
        await avatars.RemoveAsync(UserId, FamilyId, cancellationToken) ? NoContent() : NotFound();
}
