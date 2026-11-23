using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using LanguageLearning.Tests.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LanguageLearning.Tests.Diagnostics;

public sealed class RequestDiagnosticsTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task ExceptionResponseAndRequestLogsShareTraceWithoutExposingRequestValues()
    {
        using var logs = new CapturedLogs();
        using var client = fixture.CreateDiagnosticsClient<DiagnosticsProbeController>(logs);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/diagnostic-probe/private-path?token=private-query");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer private-header");
        request.Content = new StringContent("private-body");

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        string trace = problem.RootElement.GetProperty("traceId").GetString()!;

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(string.IsNullOrWhiteSpace(trace));
        Assert.Contains(logs.Entries, entry => entry.Message.Contains(trace) &&
            entry.Message.Contains("InvalidOperationException") && entry.Error is null);
        Assert.Contains(logs.Entries, entry => entry.Message.Contains(trace) &&
            entry.Message.Contains("diagnostic-probe/{value} returned 500"));

        string logText = string.Join("\n", logs.Entries.Select(entry => $"{entry.Message} {entry.Error}"));
        foreach (string value in new[] { "private-path", "private-query", "private-header", "private-body", "private-exception" })
        {
            Assert.DoesNotContain(value, body);
            Assert.DoesNotContain(value, logText);
        }
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<(string Message, Exception? Error)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Sink(Entries);

        public void Dispose() { }

        private sealed class Sink(ConcurrentQueue<(string, Exception?)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => entries.Enqueue((formatter(state, exception), exception));
        }
    }
}

[ApiController]
[AllowAnonymous]
[Route("diagnostic-probe")]
public sealed class DiagnosticsProbeController : ControllerBase
{
    [HttpPost("{value}")]
    public IActionResult Fail() => throw new InvalidOperationException("private-exception");
}
