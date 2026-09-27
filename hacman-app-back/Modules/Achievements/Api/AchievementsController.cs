using hacman_app_back.Modules.Achievements.Application;
using hacman_app_back.Modules.Identity.Api;
using hacman_app_back.Modules.Identity.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_back.Modules.Achievements.Api;

/// <summary>
/// Provides access to the achievement catalogue and the authenticated player's progress.
/// </summary>
[ApiController]
[Route("api/v1/achievements")]
public class AchievementsController(
    IAchievementCatalogProvider catalogProvider,
    IAchievementProgressService progressService,
    IUserProvider userProvider,
    IAuthClaimsParser authClaimsParser,
    ILogger<AchievementsController> logger) : ControllerBase
{
    /// <summary>Returns the full 100-achievement catalogue. Public — no authentication required.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCatalog()
    {
        logger.LogInformation("GetCatalog called");

        try
        {
            var result = (await catalogProvider.GetCatalogAsync()).ToList();
            logger.LogInformation("GetCatalog: returned {Count} definitions", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetCatalog: unhandled error");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Returns the authenticated user's progress across every achievement in the catalogue
    /// (achievements not yet touched come back at value 0 / level 0, not omitted).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyProgress()
    {
        var (externalAuthId, _) = authClaimsParser.ParseClaims(User);

        if (string.IsNullOrEmpty(externalAuthId))
        {
            logger.LogWarning("GetMyProgress: missing or invalid sub claim");
            return Unauthorized(new { message = "Invalid token claims: sub is missing or invalid" });
        }

        var user = await userProvider.GetByExternalAuthIdAsync(externalAuthId);
        if (user == null)
        {
            logger.LogWarning("GetMyProgress: no user found for externalAuthId={ExternalAuthId}", externalAuthId);
            return Unauthorized(new { message = "User is not mapped to this identity" });
        }

        logger.LogInformation("GetMyProgress: userId={UserId}", user.Id);

        try
        {
            var result = (await progressService.GetUserProgressAsync(user.Id)).ToList();
            logger.LogInformation("GetMyProgress: userId={UserId}, returned {Count} entries", user.Id, result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetMyProgress: unhandled error for userId={UserId}", user.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}
