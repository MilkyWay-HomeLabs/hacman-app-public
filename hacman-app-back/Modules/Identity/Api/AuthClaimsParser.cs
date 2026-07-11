using System.Security.Claims;

namespace hacman_app_back.Modules.Identity.Api;

public class AuthClaimsParser : IAuthClaimsParser
{
    public (string? externalAuthId, string? email) ParseClaims(ClaimsPrincipal user)
    {
        var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(subClaim))
        {
            return (null, null);
        }

        var parts = subClaim.Split(',');
        var externalAuthId = parts[0].Trim();
        var email = parts.Length > 1 ? parts[1].Trim() : null;

        if (string.IsNullOrEmpty(email))
        {
            email = user.FindFirst(ClaimTypes.Email)?.Value
                    ?? user.FindFirst("email")?.Value;
        }

        return (externalAuthId, email);
    }
}