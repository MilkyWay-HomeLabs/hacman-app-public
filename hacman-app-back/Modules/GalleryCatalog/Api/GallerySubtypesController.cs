using hacman_app_back.Modules.GalleryCatalog.Application;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_back.Modules.GalleryCatalog.Api;

/// <summary>
/// Controller responsible for handling requests related to gallery subtypes.
/// </summary>
[ApiController]
[Route("api/v1/galleries/subtypes")]
public class GallerySubtypesController(IGallerySubtypeService service, ILogger<GallerySubtypesController> logger)
    : ControllerBase
{
    /// <summary>
    /// Retrieves all gallery subtypes.
    /// </summary>
    /// <returns>An enumerable of gallery subtype name strings.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> Get()
    {
        logger.LogInformation("Get: retrieving all gallery subtypes");
        var list = await service.GetAllAsync();
        return Ok(list);
    }
}