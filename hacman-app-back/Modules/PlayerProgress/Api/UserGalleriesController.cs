using hacman_app_back.Modules.Identity.Api;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.PlayerProgress.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_back.Modules.PlayerProgress.Api;

/// <summary>
/// Manages gallery assignments for users.
/// </summary>
[ApiController]
[Route("api/v1/user-galleries")]
public class UserGalleriesController(
    IUserGalleryProvider provider,
    IUserProvider userProvider,
    IAuthClaimsParser authClaimsParser,
    ILogger<UserGalleriesController> logger) : ControllerBase
{
    /// <summary>
    /// Returns galleries for a user.
    /// When <paramref name="galleryId"/> is provided, returns the single user-gallery record for that gallery.
    /// When <paramref name="userId"/> is provided, returns all galleries for that user.
    /// Otherwise returns galleries for the authenticated user derived from the JWT.
    /// </summary>
    /// <param name="userId">Optional explicit user identifier.</param>
    /// <param name="galleryId">Optional gallery identifier; takes priority over userId.</param>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserGalleries([FromQuery] Guid? userId, [FromQuery] Guid? galleryId)
    {
        if (galleryId.HasValue)
        {
            logger.LogInformation("GetUserGalleries: fetching by galleryId={GalleryId}", galleryId.Value);
            var resultByGallery = await provider.GetByGalleryIdAsync(galleryId.Value);
            return Ok(resultByGallery);
        }

        Guid targetUserId;

        if (userId.HasValue)
        {
            targetUserId = userId.Value;
        }
        else
        {
            var (externalAuthId, _) = authClaimsParser.ParseClaims(User);

            if (string.IsNullOrWhiteSpace(externalAuthId))
            {
                logger.LogWarning("GetUserGalleries: missing or invalid sub claim");
                return Unauthorized(new { message = "Invalid token claims: sub is missing or invalid" });
            }

            var user = await userProvider.GetByExternalAuthIdAsync(externalAuthId);
            if (user == null)
            {
                logger.LogWarning("GetUserGalleries: no user found for externalAuthId={ExternalAuthId}",
                    externalAuthId);
                return Unauthorized(new { message = "User is not mapped to this identity" });
            }

            targetUserId = user.Id;
        }

        logger.LogInformation("GetUserGalleries: fetching by userId={UserId}", targetUserId);
        var result = await provider.GetAllByUserIdAsync(targetUserId);
        return Ok(result);
    }
}