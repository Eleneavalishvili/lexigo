using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace LanguageLearning.Api.Authentication;

public static class TrustedProxy
{
    public static ForwardedHeadersOptions? Configure(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        if (!IPAddress.TryParse(address, out var proxy) ||
            proxy.Equals(IPAddress.Any) || proxy.Equals(IPAddress.IPv6Any))
        {
            throw new InvalidOperationException("ReverseProxy:Address must be the immediate proxy's IP address.");
        }

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
            RequireHeaderSymmetry = true,
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownProxies.Add(proxy);
        return options;
    }
}
