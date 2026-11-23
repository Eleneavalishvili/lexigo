using System.Diagnostics;

namespace LanguageLearning.Api.Diagnostics;

public sealed class RequestSummaryMiddleware(RequestDelegate next, ILogger<RequestSummaryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        long started = Stopwatch.GetTimestamp();
        string route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
        await next(context);
        logger.LogInformation("Request {RouteTemplate} returned {StatusCode} in {ElapsedMs} ms ({TraceId})",
            route, context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            context.TraceIdentifier);
    }
}
