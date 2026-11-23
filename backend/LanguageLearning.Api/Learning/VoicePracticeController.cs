using LanguageLearning.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LanguageLearning.Api.Learning;

[ApiController]
[Authorize]
[EnableRateLimiting("learning")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/me/voice-practice/{language}")]
public sealed class VoicePracticeController(SubscriptionQueries subscriptions) : ControllerBase
{
    private static readonly string[] Meanings = ["coffee", "milk", "water"];
    private static readonly Dictionary<string, string[]> Phrases = new(StringComparer.Ordinal)
    {
        ["es"] = ["café", "leche", "agua"],
        ["it"] = ["caffè", "latte", "acqua"],
        ["de"] = ["Kaffee", "Milch", "Wasser"],
        ["ru"] = ["кофе", "молоко", "вода"]
    };

    [HttpGet]
    [ProducesResponseType<VoicePracticeItem[]>(200)]
    public async Task<IActionResult> List(string language, CancellationToken cancellationToken)
    {
        if (!await HasAccess(cancellationToken)) return PremiumRequired();
        if (!Phrases.TryGetValue(language, out var phrases)) return NotFound();
        var items = phrases.Select((phrase, index) =>
        {
            var choices = Meanings.ToArray();
            Random.Shared.Shuffle(choices);
            return new VoicePracticeItem(index, phrase, choices);
        }).ToArray();
        Random.Shared.Shuffle(items);
        return Ok(items);
    }

    [HttpPost("{id:int}/answers")]
    [RequestSizeLimit(1024)]
    [ProducesResponseType<VoicePracticeFeedback>(200)]
    public async Task<IActionResult> Check(string language, int id, VoicePracticeAnswer request,
        CancellationToken cancellationToken)
    {
        if (!await HasAccess(cancellationToken)) return PremiumRequired();
        if (!Phrases.TryGetValue(language, out var phrases) || id < 0 || id >= phrases.Length) return NotFound();
        if (!Meanings.Contains(request.Choice, StringComparer.Ordinal))
            return Problem(statusCode: 400, title: "Choose one of the listed meanings.");
        return Ok(new VoicePracticeFeedback(request.Choice == Meanings[id], Meanings[id]));
    }

    private Task<bool> HasAccess(CancellationToken cancellationToken) =>
        subscriptions.HasAsync(Guid.Parse(User.FindFirst("sub")!.Value), "PremiumListeningPractice", cancellationToken);

    private IActionResult PremiumRequired() => Problem(statusCode: 403, title: "Voice practice requires Premium.");
}

public sealed record VoicePracticeItem(int Id, string Phrase, string[] Choices);
public sealed record VoicePracticeAnswer(string Choice);
public sealed record VoicePracticeFeedback(bool Correct, string Meaning);
