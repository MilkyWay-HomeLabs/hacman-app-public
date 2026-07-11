using hacman_app_back.Modules.LevelTemplate.Application;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_back.Modules.LevelTemplate.Api;

[ApiController]
[Route("api/v1/maze/levels")]
public class MazeLevelController(
    IMazeLevelProvider provider,
    ILogger<MazeLevelController> logger
) : ControllerBase
{
    [HttpGet("{galleryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMazeLevelsByGalleryId(Guid galleryId)
    {
        logger.LogInformation(
            "GetMazeLevelsByGalleryId: galleryId={GalleryId}",
            galleryId.ToString()
        );

        try
        {
            var result = await provider.GetMazeLevelsByGalleryId(galleryId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetMazeLevelsByGalleryId: unhandled error for galleryId={GalleryId}", galleryId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}