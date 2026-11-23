using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LanguageLearning.Tests.Accounts;

internal sealed class SessionTestBrowser(HttpClient client) : IDisposable
{
    private readonly CookieContainer cookies = new();

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? bearer = null, bool csrf = false)
    {
        string? requestToken = null;
        if (csrf)
        {
            using var setup = await SendAsync(HttpMethod.Get, "/api/v1/auth/csrf", bearer: bearer);
            setup.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await setup.Content.ReadAsStringAsync());
            requestToken = document.RootElement.GetProperty("requestToken").GetString();
        }
        var uri = new Uri(client.BaseAddress!, path);
        var request = new HttpRequestMessage(method, uri);
        string cookieHeader = cookies.GetCookieHeader(uri);
        if (cookieHeader.Length > 0) request.Headers.Add("Cookie", cookieHeader);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (requestToken is not null) request.Headers.Add("X-XSRF-TOKEN", requestToken);
        if (body is not null) request.Content = body as HttpContent ?? JsonContent.Create(body);
        var response = await client.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var headers))
            foreach (string header in headers) cookies.SetCookies(uri, header);
        return response;
    }

    public string? RefreshToken => cookies.GetCookies(new Uri(client.BaseAddress!, "/api/v1/auth/refresh"))["lexigo.refresh"]?.Value;
    public void ReplaceRefresh(string value) => cookies.Add(new Cookie("lexigo.refresh", value, "/api/v1/auth", "localhost"));
    public void Dispose() => client.Dispose();
}
