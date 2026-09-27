using System.Security.Claims;

namespace hacman_app_back.Modules.Identity.Api;

public interface IAuthClaimsParser
{
    (string? externalAuthId, string? email) ParseClaims(ClaimsPrincipal user);
}