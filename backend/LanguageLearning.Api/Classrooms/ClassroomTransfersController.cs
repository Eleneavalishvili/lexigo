using System.ComponentModel.DataAnnotations;
using LanguageLearning.Infrastructure.Classrooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Classrooms;

[ApiController]
[Authorize]
[Route("api/v1/classes")]
[EnableRateLimiting("content-write")]
[RequestSizeLimit(8192)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ClassroomTransfersController(ClassroomTransferService transfers) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid FamilyId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpPost("{id:guid}/transfer")]
    [ProducesResponseType<ClassTransferLink>(200)]
    public async Task<IActionResult> Create(Guid id, CreateClassTransfer request, CancellationToken token) =>
        await transfers.CreateAsync(UserId, FamilyId, id, request.Email, HttpContext.TraceIdentifier, token) is { } result
            ? Ok(result) : NotFound();

    [HttpPost("transfers/preview")]
    [ProducesResponseType<ClassTransferPreview>(200)]
    public async Task<IActionResult> Preview(ClassTransferRequest request, CancellationToken token) =>
        await transfers.PreviewAsync(UserId, FamilyId, request.Token, token) is { } result ? Ok(result) : NotFound();

    [HttpPost("transfers/accept")]
    [ProducesResponseType<ClassroomWriteResponse>(200)]
    public async Task<IActionResult> Accept(ClassTransferRequest request, CancellationToken token)
    {
        var result = await transfers.AcceptAsync(UserId, FamilyId, request.Token, HttpContext.TraceIdentifier, token);
        if (result.Forbidden) return Forbid();
        if (result.Missing) return NotFound();
        if (result.Error is { } error) return Problem(statusCode: 409, title: error);
        return Ok(new ClassroomWriteResponse(result.Id!.Value));
    }
}

public sealed record CreateClassTransfer([Required, EmailAddress, MaxLength(256)] string Email);
public sealed record ClassTransferRequest([Required, MaxLength(2048)] string Token);
