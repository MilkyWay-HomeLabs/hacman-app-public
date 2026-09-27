using hacman_app_back.Modules.GalleryCatalog.Application;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_back.Modules.GalleryCatalog.Api;

/// <summary>
/// Controller responsible for handling requests related to gallery themes.
/// </summary>
[ApiController]
[Route("api/v1/galleries/themes")]
public class GalleryThemesController(IGalleryThemeService service, ILogger<GalleryThemesController> logger)
    : ControllerBase
{
    /// <summary>
    /// Retrieves all gallery themes.
    /// </summary>
    /// <returns>An enumerable of gallery theme name strings.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> Get()
    {
        logger.LogInformation("Get: retrieving all gallery themes");
        var list = await service.GetAllAsync();
        return Ok(list);
    }
}