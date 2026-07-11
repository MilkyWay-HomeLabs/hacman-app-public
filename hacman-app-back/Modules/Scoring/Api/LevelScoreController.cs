using hacman_app_back.Modules.Identity.Api;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Scoring.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_back.Modules.Scoring.Api;

/// <summary>
/// Provides access to level score data.
/// </summary>
[ApiController]
[Route("api/v1/level-scores")]
public class LevelScoreController(
    ILevelScoreService service,
    IUserProvider userProvider,
    IAuthClaimsParser authClaimsParser,
    ILogger<LevelScoreController> logger) : ControllerBase
{
    /// <summary>
    /// Returns a paginated list of scores for a gallery, optionally filtered by difficulty.
    /// </summary>
    /// <param name="galleryId">The gallery to query scores for.</param>
    /// <param name="difficulty">Optional difficulty filter (e.g. EASY, NORMAL, HARD).</param>
    /// <param name="pageNumber">1-based page number (default 1).</param>
    /// <param name="pageSize">Number of records per page (default 10).</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetScores(
        [FromQuery] Guid galleryId,
        [FromQuery] string? difficulty = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        logger.LogInformation(
            "GetScores: galleryId={GalleryId}, difficulty={Difficulty}, page={Page}/{Size}",
            galleryId, difficulty ?? "any", pageNumber, pageSize);

        try
        {
            var result = (await service.GetScores(galleryId, difficulty, pageNumber, pageSize)).ToList();
            logger.LogInformation("GetScores: returned {Count} records", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetScores: unhandled error for galleryId={GalleryId}", galleryId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Returns scores for a specific user, optionally filtered by gallery and difficulty.
    /// </summary>
    /// <param name="userId">The user whose scores are requested.</param>
    /// <param name="galleryId">Optional gallery filter.</param>
    /// <param name="difficulty">Optional difficulty filter.</param>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetScoresByUserId(
        Guid userId,
        [FromQuery] Guid? galleryId = null,
        [FromQuery] string? difficulty = null)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest("User ID cannot be empty.");
        }

        logger.LogInformation(
            "GetScoresByUserId: userId={UserId}, galleryId={GalleryId}, difficulty={Difficulty}",
            userId, galleryId?.ToString() ?? "any", difficulty ?? "any");

        try
        {
            var result = await service.GetScoresByUserId(userId, galleryId, difficulty);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetScoresByUserId: unhandled error for userId={UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Records a new score for the authenticated user, and updates their cached best time for
    /// the gallery if this is a new record.
    /// </summary>
    /// <param name="request">The gallery, difficulty, and completion time to record.</param>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ScoreEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(CreateLevelScoreRequest request)
    {
        var (externalAuthId, _) = authClaimsParser.ParseClaims(User);

        if (string.IsNullOrEmpty(externalAuthId))
        {
            logger.LogWarning("Create: missing or invalid sub claim");
            return Unauthorized(new { message = "Invalid token claims: sub is missing or invalid" });
        }

        var user = await userProvider.GetByExternalAuthIdAsync(externalAuthId);
        if (user == null)
        {
            logger.LogWarning("Create: no user found for externalAuthId={ExternalAuthId}", externalAuthId);
            return Unauthorized(new { message = "User is not mapped to this identity" });
        }

        if (request.GalleryId == Guid.Empty)
        {
            return BadRequest(new { message = "GalleryId is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Difficulty))
        {
            return BadRequest(new { message = "Difficulty is required" });
        }

        if (request.TimeMs <= 0)
        {
            return BadRequest(new { message = "TimeMs must be positive" });
        }

        logger.LogInformation(
            "Create: userId={UserId}, galleryId={GalleryId}, difficulty={Difficulty}, timeMs={TimeMs}",
            user.Id, request.GalleryId, request.Difficulty, request.TimeMs);

        try
        {
            var created = await service.CreateScore(user.Id, user.Username ?? string.Empty, request.GalleryId,
                request.Difficulty, request.TimeMs);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Create: invalid request");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Create: unhandled error for userId={UserId}, galleryId={GalleryId}",
                user.Id, request.GalleryId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}