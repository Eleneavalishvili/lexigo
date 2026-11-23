using System.Net;
using LanguageLearning.Api.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Tests.Diagnostics;

public sealed class TrustedProxyTests
{
    [Theory]
    [InlineData("172.29.42.10", true)]
    [InlineData("::ffff:172.29.42.10", true)]
    [InlineData("172.29.42.11", false)]
    [InlineData("127.0.0.1", false)]
    public async Task ForwardsOnlyFromTheConfiguredPeer(string peer, bool trusted)
    {
        var options = TrustedProxy.Configure("172.29.42.10")!;
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.12, 203.0.113.23";
        context.Request.Headers["X-Forwarded-Proto"] = "http, https";
        context.Request.Headers["X-Forwarded-Host"] = "unexpected.example";
        context.Request.Host = new HostString("lexigo.example");
        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        await middleware.Invoke(context);

        Assert.Equal(trusted ? "https" : "http", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse(trusted ? "203.0.113.23" : peer), context.Connection.RemoteIpAddress);
        Assert.Equal("lexigo.example", context.Request.Host.Host);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("172.29.42.0/24")]
    public void RejectsBroadOrInvalidAddresses(string address) =>
        Assert.Throws<InvalidOperationException>(() => TrustedProxy.Configure(address));
}
