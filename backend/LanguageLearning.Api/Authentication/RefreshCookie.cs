namespace LanguageLearning.Api.Authentication;

public sealed class RefreshCookie(IWebHostEnvironment environment)
{
    public string Name => environment.IsDevelopment() ? "lexigo.refresh" : "__Host-lexigo.refresh";
    private string CookiePath => environment.IsDevelopment() ? "/api/v1/auth" : "/";

    public string? Read(HttpRequest request) => request.Cookies[Name];

    public void Write(HttpResponse response, string token, DateTimeOffset expires) => response.Cookies.Append(Name, token, new CookieOptions
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true
    });

    public void Clear(HttpResponse response) => response.Cookies.Delete(Name, new CookieOptions
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = CookiePath
    });
}
