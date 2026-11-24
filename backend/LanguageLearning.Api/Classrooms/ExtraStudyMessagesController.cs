using LanguageLearning.Infrastructure.Classrooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Classrooms;

[ApiController, Authorize]
[Route("api/v1/extra-study/requests/{requestId:guid}/messages")]
[EnableRateLimiting("learning")]
[RequestSizeLimit(16384)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExtraStudyMessagesController(ExtraStudySpace spaces) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(Guid requestId, Guid? before, CancellationToken token)
    {
        var page = await spaces.MessagesAsync(UserId, FamilyId, requestId, before, token);
        return page is null ? NotFound() : Ok(page);
    }

    [HttpPost]
    [EnableRateLimiting("message-send")]
    public async Task<IActionResult> Send(Guid requestId, SendStudyMessage input, CancellationToken token)
    {
        var result = await spaces.SendMessageAsync(UserId, FamilyId, requestId, input, token);
        return result.Missing ? NotFound() : result.Error is { } error
            ? Problem(statusCode: 409, title: error) : Ok(new { id = result.Id });
    }
}
