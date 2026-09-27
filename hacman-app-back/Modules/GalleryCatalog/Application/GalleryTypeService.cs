using hacman_app_back.Modules.GalleryCatalog.Infrastructure;

namespace hacman_app_back.Modules.GalleryCatalog.Application;

/// <summary>
/// Service class responsible for managing gallery types.
/// </summary>
public class GalleryTypeService : IGalleryTypeService
{
    private readonly IGalleryTypeRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GalleryTypeService"/> class.
    /// </summary>
    /// <param name="repository">The repository used to access gallery type data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the repository is null.</exception>
    public GalleryTypeService(IGalleryTypeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Retrieves all gallery types asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable of gallery type strings.</returns>
    public Task<IEnumerable<string>> GetAllAsync() => _repository.GetAllAsync();
}