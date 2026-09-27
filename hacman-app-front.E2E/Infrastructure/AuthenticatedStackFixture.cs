using System;
using System.Net.Http;
using Microsoft.Playwright;
using Xunit;

namespace hacman_app_front.E2E.Infrastructure;

/// <summary>
/// Fixture for FE-2+ authenticated E2E tests. Runs against the real, already-running Hacman
/// deployment served through Traefik at <c>milkyway.test</c> — deliberately NOT a locally
/// self-started <c>localhost</c> stack.
///
/// This targeting is load-bearing, not incidental:
/// <list type="bullet">
///   <item>Authentication is an <b>httpOnly</b> <c>accessToken</c> JWT cookie scoped to the
///   <c>milkyway.test</c> domain (set by <c>AuthController.BuildCookieOptions</c>:
///   <c>HttpOnly=true, Secure=true, SameSite=Lax, Path="/"</c>). Cookie matching keys off the
///   hostname the browser uses, so the cookie is only ever sent when the page is served from
///   <c>milkyway.test</c>. <c>localhost</c>/<c>127.0.0.1</c> can never carry it, even though
///   <c>milkyway.test</c> itself resolves to loopback in this environment (see <c>/etc/hosts</c>) —
///   the resolved IP is irrelevant, the host label is what the cookie is bound to.</item>
///   <item>On the real deployment the frontend (<c>/hacman/app</c>) and the API
///   (<c>/hacman/api</c>) are the <b>same origin</b> (both <c>milkyway.test:443</c>, path-routed by
///   Traefik), so the cookie flows to <c>app.js</c>'s browser-side API calls with no CORS involved
///   — reproducing production exactly. A localhost self-start would be a different origin per port,
///   needing both a CORS exception the deployed backend doesn't grant for a milkyway.test origin
///   and a fake cookie domain — i.e. it would exercise a flow the real app never uses.</item>
/// </list>
///
/// The cookie is injected via Playwright's <c>BrowserContext.AddCookiesAsync</c>, which operates at
/// the browser level: it can set an HttpOnly cookie that page JS (<c>document.cookie</c>) can
/// neither read nor write, matching the real token exactly. That is also why a real login can't be
/// scripted here (it redirects to Nebula and the token never touches JS) — injection is the only
/// automatable path.
///
/// The backend disables JWT signature validation (<c>hacman-app-back/Program.cs</c>: "Signature
/// validation is disabled for Nebula integration"), so the hand-built <see cref="TestJwt"/> is
/// accepted without a real Nebula login round-trip.
///
/// Base URL defaults to the running test deployment (<c>https://milkyway.test/hacman/app/</c>) and
/// is overridable via <see cref="BaseUrlEnvVar"/>. The deployment lifecycle is the user's (managed
/// via Docker/Traefik); this fixture only asserts it is reachable and fails fast with guidance if
/// not. HTTPS certificate errors are ignored by default (milkyway.test sits behind Traefik with a
/// self-signed/private-CA cert), overridable via <see cref="IgnoreHttpsErrorsEnvVar"/>.
/// </summary>
public sealed class AuthenticatedStackFixture : IAsyncLifetime
{
    private const string BaseUrlEnvVar = "HACMAN_E2E_BASE_URL";
    private const string IgnoreHttpsErrorsEnvVar = "HACMAN_E2E_IGNORE_HTTPS_ERRORS";
    private const string DefaultBaseUrl = "https://milkyway.test/hacman/app/";

    private IPlaywright? _playwright;
    private string _cookieDomain = null!;
    private bool _cookieSecure;
    private bool _ignoreHttpsErrors;

    public string BaseUrl { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        BaseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvVar) ?? DefaultBaseUrl;

        var baseUri = new Uri(BaseUrl);
        _cookieDomain = baseUri.Host;
        _cookieSecure = baseUri.Scheme == Uri.UriSchemeHttps;

        // Default to ignoring cert errors because the default target (milkyway.test) is behind a
        // self-signed/private-CA cert; allow an explicit opt-out for a trusted-cert deployment.
        _ignoreHttpsErrors =
            !bool.TryParse(Environment.GetEnvironmentVariable(IgnoreHttpsErrorsEnvVar), out var v) || v;

        await EnsureDeploymentReachableAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            // See WebAppFixture for why this is required in this environment.
            Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" },
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    /// <summary>
    /// Creates a browser context with <paramref name="accessToken"/> pre-seeded as the httpOnly
    /// <c>accessToken</c> cookie the frontend's auth pipeline and the backend's
    /// <c>OnMessageReceived</c> JWT event both read. Cookie attributes mirror the real one set by
    /// <c>AuthController</c> (<c>HttpOnly</c>, <c>Secure</c> on https, <c>SameSite=Lax</c>,
    /// <c>Path=/</c>, domain-scoped to the deployment host) so the browser treats it identically.
    /// </summary>
    public async Task<IBrowserContext> NewAuthenticatedContextAsync(string accessToken)
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = _ignoreHttpsErrors,
        });

        await context.AddCookiesAsync(new[]
        {
            new Cookie
            {
                Name = "accessToken",
                Value = accessToken,
                Domain = _cookieDomain,
                Path = "/",
                HttpOnly = true,
                Secure = _cookieSecure,
                SameSite = SameSiteAttribute.Lax,
            },
        });

        return context;
    }

    private async Task EnsureDeploymentReachableAsync()
    {
        using var handler = new HttpClientHandler
        {
            // Match the browser context: the default target's cert is self-signed.
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

        try
        {
            using var response = await http.GetAsync(BaseUrl);
            // Any HTTP response (even non-2xx) means the deployment is up and routing.
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Authenticated E2E deployment at '{BaseUrl}' is not reachable ({ex.Message}). " +
                $"Bring the Hacman deployment up (Docker/Traefik) or set {BaseUrlEnvVar} to a running instance. " +
                "A dev deployment returning 502 means its upstream containers are down.",
                ex);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class AuthenticatedStackCollection : ICollectionFixture<AuthenticatedStackFixture>
{
    public const string Name = "Authenticated Stack";
}
