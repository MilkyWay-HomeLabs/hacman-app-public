using hacman_app_back.Modules.GalleryCatalog.Application;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_back.Modules.GalleryCatalog.Api;

/// <summary>
/// Controller responsible for handling requests related to gallery types.
/// </summary>
[ApiController]
[Route("api/v1/galleries/types")]
public class GalleryTypesController(IGalleryTypeService service, ILogger<GalleryTypesController> logger)
    : ControllerBase
{
    /// <summary>
    /// Retrieves all gallery types.
    /// </summary>
    /// <returns>An enumerable of gallery type name strings.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> Get()
    {
        logger.LogInformation("Get: retrieving all gallery types");
        var list = await service.GetAllAsync();
        return Ok(list);
    }
}