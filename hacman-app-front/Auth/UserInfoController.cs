using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_front.Auth;

[ApiController]
[Route("auth/user")]
public class UserInfoController(ILogger<UserInfoController> logger) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var bootstrap = await BuildBootstrapResponse();
        if (bootstrap.Result != null)
        {
            return bootstrap.Result;
        }

        return Ok(bootstrap.Data!.HacmanData);
    }

    [HttpGet("bootstrap")]
    public async Task<IActionResult> Bootstrap()
    {
        var bootstrap = await BuildBootstrapResponse();
        if (bootstrap.Result != null)
        {
            return bootstrap.Result;
        }

        return Ok(bootstrap.Data);
    }

    private async Task<(BootstrapResponse? Data, IActionResult? Result)> BuildBootstrapResponse()
    {
        logger.LogInformation("UserInfoController bootstrap started");
        var token = Request.Cookies["accessToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("No accessToken cookie found");
            return (null, Unauthorized());
        }

        var userData = ExtractUserDataFromAccessToken(token);
        if (userData == null)
        {
            logger.LogError("Failed to extract userData from access token");
            return (null, StatusCode(502, "Failed to read user data from access token"));
        }

        logger.LogInformation("Successfully extracted userData from token: login={Login}", userData.Login);

        // Hacman data (user + galleries) is fetched client-side by app.js directly from the Hacman API.
        // Bootstrap is responsible only for token validation and returning Nebula data.
        return (new BootstrapResponse
        {
            UserData = userData,
            HacmanData = null
        }, null);
    }

    private static string DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += new string('=', (4 - normalized.Length % 4) % 4);
        var bytes = Convert.FromBase64String(normalized);
        return Encoding.UTF8.GetString(bytes);
    }

    private NebulaUserData? ExtractUserDataFromAccessToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                logger.LogWarning("Token does not have 3 parts (header.payload.signature)");
                return null;
            }

            var payload = DecodeBase64Url(parts[1]);
            logger.LogInformation("JWT payload: {Payload}", payload);

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var id = ReadClaim(root, "sub") ?? ReadClaim(root, "jti");
            var email = ReadClaim(root, "email");
            var login = ReadClaim(root, "preferred_username")
                        ?? ReadClaim(root, "login")
                        ?? ReadClaim(root, "unique_name")
                        ?? ReadClaim(root, "name")
                        ?? email?.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .FirstOrDefault()
                        ?? id;

            logger.LogInformation("Extracted claims: id={Id}, login={Login}, email={Email}", id, login, email);

            long? numericId = null;
            if (long.TryParse(id, out var parsedId))
            {
                numericId = parsedId;
            }
            else if (!string.IsNullOrWhiteSpace(id))
            {
                logger.LogWarning("Could not parse id as long: {Id}", id);
            }

            var userData = new NebulaUserData
            {
                Id = numericId,
                Login = login,
                UserName = ReadClaim(root, "name") ?? login,
                Email = email
            };

            logger.LogInformation("Built userData: Id={Id}, Login={Login}, UserName={UserName}, Email={Email}",
                userData.Id, userData.Login, userData.UserName, userData.Email);

            return userData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during access token parsing");
            return null;
        }
    }

    private static string? ReadClaim(JsonElement root, string claimName)
    {
        return root.TryGetProperty(claimName, out var claim)
            ? claim.ValueKind == JsonValueKind.String
                ? claim.GetString()
                : claim.ToString()
            : null;
    }
}

public class NebulaUserData
{
    public long? Id { get; set; }
    public string? Login { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
}

public class HacmanUserData
{
    public Guid Id { get; set; }
    public string? ExternalAuthId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
}

public class BootstrapResponse
{
    public NebulaUserData? UserData { get; set; }
    public HacmanUserData? HacmanData { get; set; }
}