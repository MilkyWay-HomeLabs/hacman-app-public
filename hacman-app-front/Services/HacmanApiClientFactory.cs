using System.Net;
using System.Net.Http.Headers;

namespace hacman_app_front.Services;

/// <summary>
/// Creates <see cref="HttpClient"/>s for the Hacman backend API with the caller's access
/// token attached as an <c>accessToken</c> cookie, replacing the three copy-pasted
/// registration blocks that used to live in Program.cs.
///
/// Token resolution order:
/// 1. The incoming request's <c>accessToken</c> cookie (SSR / circuit initialization) — when
///    found it is also persisted into <see cref="UserSessionState"/>, so later calls on the
///    same circuit (where no <see cref="HttpContext"/> exists anymore) can still authenticate.
/// 2. The circuit's <see cref="UserSessionState.AccessToken"/>.
/// </summary>
public sealed class HacmanApiClientFactory(
    IHttpContextAccessor httpContextAccessor,
    UserSessionState sessionState,
    ILogger<HacmanApiClientFactory> logger,
    Uri apiBaseUri,
    bool trustAnyServerCertificate = false)
{
    private const string AccessCookieName = "accessToken";

    public string? ResolveAccessToken()
    {
        var context = httpContextAccessor.HttpContext;
        if (context != null && context.Request.Cookies.TryGetValue(AccessCookieName, out var cookieToken))
        {
            logger.LogDebug("Access token resolved from HttpContext cookie (request path: {Path})",
                context.Request.Path);
            sessionState.AccessToken = cookieToken;
            return cookieToken;
        }

        if (!string.IsNullOrEmpty(sessionState.AccessToken))
        {
            logger.LogDebug("Access token resolved from UserSessionState (no HttpContext cookie)");
            return sessionState.AccessToken;
        }

        logger.LogDebug("No access token available in HttpContext nor UserSessionState");
        return null;
    }

    /// <summary>
    /// Builds the message handler for API calls. Certificate validation is only relaxed when
    /// explicitly opted in (Development, or TrustSelfSignedCertificates=true for deployments
    /// behind a private CA / self-signed reverse proxy).
    /// </summary>
    public HttpClientHandler CreateHandler(string? token)
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };

        if (trustAnyServerCertificate)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        if (!string.IsNullOrEmpty(token))
        {
            // Register the cookie against the server-side URI (may differ from the public URI in Docker).
            handler.CookieContainer.Add(apiBaseUri, new Cookie(AccessCookieName, token) { Path = "/" });
        }

        return handler;
    }

    public HttpClient CreateClient()
    {
        var httpClient = new HttpClient(CreateHandler(ResolveAccessToken()))
        {
            BaseAddress = apiBaseUri,
            // Increased timeout to accommodate slow DNS resolution in Docker environments.
            Timeout = TimeSpan.FromSeconds(30)
        };

        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }
}