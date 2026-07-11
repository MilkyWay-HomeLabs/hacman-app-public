using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_front.Auth;

[Route("auth")]
public class AuthController(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<AuthController> logger)
    : Controller
{
    private const string AccessCookieName = "accessToken";
    private const string RefreshCookieName = "refreshToken";
    private const string NebulaHttpClientName = "NebulaClient";

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string token,
        [FromQuery] string returnUrl)
    {
        logger.LogInformation(
            "Callback started. Code present: {HasCode}, Token present: {HasToken}, ReturnUrl: {ReturnUrl}",
            !string.IsNullOrEmpty(code), !string.IsNullOrEmpty(token), returnUrl);

        string? accessToken = null;
        string? refreshToken = null;
        int? expiresInSeconds = null;

        if (!string.IsNullOrEmpty(code))
        {
            // Callback historically uses the default (unnamed) HttpClient for the code exchange.
            var outcome = await RequestNebulaTokenAsync(clientName: string.Empty, grantType: "authorization_code",
                new KeyValuePair<string, string>("code", code));

            if (!outcome.Success)
            {
                logger.LogError("Token exchange failed: {StatusCode}. Body: {Body}", outcome.StatusCode,
                    outcome.ErrorBody);
                return StatusCode(outcome.StatusCode, $"Token exchange failed: {outcome.ErrorBody}");
            }

            using var json = outcome.Json!;
            if (json.RootElement.TryGetProperty("access_token", out var t)) accessToken = t.GetString() ?? string.Empty;
            if (json.RootElement.TryGetProperty("refresh_token", out var rt)) refreshToken = rt.GetString();
            if (json.RootElement.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var parsedExp))
            {
                expiresInSeconds = parsedExp;
            }
        }
        else if (!string.IsNullOrEmpty(token))
        {
            logger.LogInformation("Using token from query parameter");
            accessToken = token;
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            logger.LogWarning("Authentication failed: No access token found");
            return BadRequest("Authentication failed");
        }

        logger.LogInformation("Authentication successful. Setting token cookies.");
        Response.Cookies.Append(AccessCookieName, accessToken, BuildCookieOptions(expiresInSeconds));

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            // Refresh token lifetime is usually longer than access token.
            Response.Cookies.Append(RefreshCookieName, refreshToken, BuildCookieOptions(60 * 60 * 24 * 14));
        }

        return Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new { message = "Missing refresh token" });
        }

        var outcome = await RequestNebulaTokenAsync(clientName: NebulaHttpClientName, grantType: "refresh_token",
            new KeyValuePair<string, string>("refresh_token", refreshToken));

        if (!outcome.Success)
        {
            logger.LogWarning("Refresh token flow failed: {StatusCode}. Body: {Body}", outcome.StatusCode,
                outcome.ErrorBody);
            Response.Cookies.Delete(AccessCookieName, BuildCookieOptions());
            return StatusCode(outcome.StatusCode, new { message = "Token refresh failed" });
        }

        using var json = outcome.Json!;

        if (!json.RootElement.TryGetProperty("access_token", out var accessTokenElement))
        {
            return StatusCode(502, new { message = "No access token in refresh response" });
        }

        var accessToken = accessTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return StatusCode(502, new { message = "Empty access token in refresh response" });
        }

        int? expiresInSeconds = null;
        if (json.RootElement.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var parsedExp))
        {
            expiresInSeconds = parsedExp;
        }

        Response.Cookies.Append(AccessCookieName, accessToken, BuildCookieOptions(expiresInSeconds));

        if (json.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement))
        {
            var rotatedRefreshToken = refreshTokenElement.GetString();
            if (!string.IsNullOrWhiteSpace(rotatedRefreshToken))
            {
                Response.Cookies.Append(RefreshCookieName, rotatedRefreshToken, BuildCookieOptions(60 * 60 * 24 * 14));
            }
        }

        return Ok(new { refreshed = true });
    }

    /// <summary>
    /// Performs a Nebula token-endpoint POST shared by the authorization-code (Callback) and
    /// refresh-token (Refresh) flows: resolves the endpoint, attaches the client credentials and
    /// the grant-specific fields, and returns either the parsed JSON response or the failure status/body.
    /// The caller owns the returned <see cref="JsonDocument"/> and must dispose it.
    /// </summary>
    private async Task<TokenExchangeOutcome> RequestNebulaTokenAsync(
        string clientName, string grantType, params KeyValuePair<string, string>[] grantFields)
    {
        // Default Nebula token endpoint (can be overridden via NEBULA_TOKEN_ENDPOINT).
        var tokenEndpoint = config["NEBULA_TOKEN_ENDPOINT"] ?? "https://milkyway.test/nebula/api/token";

        var fields = new List<KeyValuePair<string, string>>
        {
            new("grant_type", grantType),
            new("client_id", config["NEBULA_CLIENT_ID"] ?? string.Empty),
            new("client_secret", config["NEBULA_CLIENT_SECRET"] ?? string.Empty)
        };
        fields.AddRange(grantFields);

        logger.LogInformation("Requesting Nebula token (grant_type={GrantType}) at {TokenEndpoint}", grantType,
            tokenEndpoint);

        var client = httpClientFactory.CreateClient(clientName);
        using var body = new FormUrlEncodedContent(fields);
        var resp = await client.PostAsync(tokenEndpoint, body);

        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync();
            return new TokenExchangeOutcome(false, (int)resp.StatusCode, errorBody, null);
        }

        using var stream = await resp.Content.ReadAsStreamAsync();
        var json = await JsonDocument.ParseAsync(stream);
        return new TokenExchangeOutcome(true, (int)resp.StatusCode, null, json);
    }

    private sealed record TokenExchangeOutcome(bool Success, int StatusCode, string? ErrorBody, JsonDocument? Json);

    private CookieOptions BuildCookieOptions(int? expiresInSeconds = null)
    {
        var isDev = string.Equals(config["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
        var appBasePath = config["AppBasePath"] ?? "/";

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = SameSiteMode.Lax,
            Path = "/", // Use root path to ensure cookie is sent for all app paths
            Expires = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds ?? 3600)
        };

        return options;
    }
}