using Microsoft.AspNetCore.Antiforgery;

namespace MyMind.Api.Http;

/// <summary>
/// Issues the antiforgery request token in a JS-readable <c>XSRF-TOKEN</c> cookie; Angular's
/// <c>HttpClient</c> echoes it back in the <c>X-XSRF-TOKEN</c> header (plan §7.2).
/// The token is bound to the current user, so it is re-issued whenever the user changes.
/// </summary>
internal static class XsrfToken
{
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";

    public static void Issue(HttpContext http)
    {
        var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(http);

        http.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }
}
